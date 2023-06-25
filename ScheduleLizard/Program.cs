using System;
using System.IO;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScheduleLizard
{
	// TODO: Can't schedule programming and lego together
	// TODO: list duplicate camper names
	// TODO: blacklist two-weekers
	// TODO: Report relative course interest
	// enumerate by preference major, camper minor
	class Program
	{
		const string StudentListFile = @"Input\StudentList.csv";
		const string CourseScheduleFile = @"Input\CourseSchedule.csv";
		const string StudentPreferenceFile = @"Input\StudentPreferences.csv";

		const string SurveyFilePath = @"Output\StudentCourseSurveyPrintable.txt";
		const string StudentPreferenceTemplateFile = @"Output\StudentPreferenceTemplate.csv";
		const string RasterByClassPrintable = @"Output\ByClassPrintable.txt";
		const string RasterByTeacherSummary = @"Output\ByTeacherSummary.txt";
		const string ByStudentPrintable = @"Output\ByStudentPrintable.txt";
		const string ByStudent = @"Output\ByStudent.txt";

		const int RandomSeed = 100; // Deterministic output
		const char MSWordPageBreak = '\f';

		static void Main(string[] args)
		{
			try
			{
				var courses = InputCourses().ToArray();
				//CleanOutputFolder();

				//if (!File.Exists(SurveyFilePath))
					//var students = InputStudents().ToArray();
					//WriteSurvey(courses, students);
					//WritePreferenceTemplate(courses, students);
				//else
					var studentPreferences = InputPreferences().ToArray();

					SummarizeCoursePopularity(courses, studentPreferences);

					Validate(courses, studentPreferences);
					CalculateSchedule(courses, studentPreferences);
					WriteStudentSchedules(studentPreferences);
					WriteClassSchedules(courses);
				//
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
			}

			Console.ReadKey(true);
		}

		//static void CleanOutputFolder()
		//{
		//	if (Directory.Exists(OutputPath))
		//	{
		//		Directory.Delete(OutputPath, true);
		//	}

		//	Directory.CreateDirectory(OutputPath);
		//}

		static Course[] InputCourses()
		{
			var lines = File.ReadAllLines(CourseScheduleFile)
				.Where(l => !l.StartsWith("//")) // Allow comment lines
				.Skip(1) // ignore header line
				.Select(l => l.Split(','))
				.ToArray();
			var schedule = lines.Select(l => new Course() { Name = l[0], Capacity = int.Parse(l[1]), Period = int.Parse(l[2]), Teacher = l[3], Room = l[4], CanRetake = bool.Parse(l[5]) }).ToArray();

			return schedule;
		}

		static IEnumerable<Student> InputStudents()
		{
			return File.ReadAllLines(StudentListFile)
				.Where(l => !l.StartsWith("//")) // Skip comment lines
				.Select(l => l.Split(','))
				.Select(n => new Student() { Name = n[0], Family = n[1] })
				.ToArray();
		}

		static void WritePreferenceTemplate(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			var content = new StringBuilder();
			content.AppendLine(string.Join(",", courses.Select(c => c.Name).Distinct().Prepend("priority").Prepend("studentName")));

			foreach (var student in students)
			{
				content.AppendLine(string.Join(",", student.Name, 0));
			}

			File.WriteAllText(StudentPreferenceTemplateFile, content.ToString());
		}

		static IEnumerable<Student> InputPreferences()
		{
			var lines = File.ReadAllLines(StudentPreferenceFile)
				.Where(l => !l.StartsWith("//")) // Skip comment lines
				.Select(l =>l.Split(','))
				.ToArray();
			var courses = lines[0].Skip(2).ToArray();
			var studentLines = lines.Skip(1).OrderBy(s => int.Parse(s[0])).ToArray();

			foreach (string[] studentLine in studentLines)
			{
				var name = studentLine[0];
				var priority = int.Parse(studentLine[1]);
				string[] preferences;

				if (studentLine.Length != courses.Length + 2)
				{
					Console.WriteLine($"WARNING: Student {name} has the wrong number of course preferences");
					preferences = new string[0];
				}
				else
				{
					preferences = studentLine
						.Skip(2) // Skip priority and name
						.Select(rank =>
							rank == "NO" || rank == "0" ? 999 :
							rank == "" ? 50 :
							int.Parse(rank))
						.Concat(Enumerable.Repeat(1, courses.Length))
						.Zip(courses, (p, c) => new { p, c })
						.Shuffle(RandomSeed)
						.OrderBy(z => z.p)
						.Select(z => z.c)
						.ToArray();
				}

				var student = new Student() { Name = name, Priority = priority, CoursePreferencesInOrder = preferences };

				yield return student;
			}
		}

		static void SummarizeCoursePopularity(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			var popularity = students.SelectMany(s => s.CoursePreferencesInOrder.Take(4)).GroupBy(c => c).ToDictionary(c => c.Key, c => c.Count());

			foreach (var pop in popularity.OrderByDescending(p => p.Value))
			{
				Console.WriteLine($"{pop.Key}: {pop.Value}");
			}
		}

		static void Validate(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			// No two campers have the same name
			var dupeStudents = students.GroupBy(s => s.Name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
			if (dupeStudents.Any())
			{
				throw new Exception($"Duplicate Students: {string.Join(", ", dupeStudents)}");
			}

			var periods = courses.Max(c => c.Period);
			// There's enough capacity at each period
			for (int i = 1; i <= periods; i++)
			{
				var cap = courses.Where(c => c.Period == i).Sum(c => c.Capacity);
				if (cap < students.Count())
				{
					throw new Exception($"Period #{i} has course capacity {cap} but there are {students.Count()} students in camp.");
				}
			}

			// Student preferences match available courses
			var missingClasses = students.SelectMany(s => s.CoursePreferencesInOrder).Distinct().Except(courses.Select(c => c.Name)).ToArray();
			if (missingClasses.Any())
			{
				throw new Exception($"{string.Join(", ", missingClasses)} classes were requested but are not offered");
			}

			// Only one class per period per teacher
			var overBookedTeachers = courses.GroupBy(c => new { c.Period, c.Teacher }).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
			foreach (var overTeacher in overBookedTeachers)
			{
				throw new Exception($"{overTeacher.Teacher} is overbook in period {overTeacher.Period}.");
			}

			// Only one class per period per room
			var overBookedRooms = courses.GroupBy(c => new { c.Period, c.Room }).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
			foreach (var overRoom in overBookedRooms)
			{
				throw new Exception($"{overRoom.Room} is overbook in period {overRoom.Period}.");
			}
		}

		static void CalculateSchedule(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			var periods = courses.Max(c => c.Period);
			var courseCount = courses.GroupBy(c => c.Name).Count();
			var score = 0;

			for (var preferenceIndex = 0; preferenceIndex < courseCount; preferenceIndex++)
			{
				foreach (var student in students.OrderByDescending(s => s.Priority))
				{
					var prefered = student.CoursePreferencesInOrder[preferenceIndex];

					if (student.ClassSchedule.Count < periods)
					{
						foreach (var course in courses.OrderBy(c => c.Students.Count)) // Balance class sizes
						//foreach (var course in courses.OrderBy(c => c.Students.Count)) // Packed tight
						{
							if (course.Name == prefered
								&& course.Students.Count < course.Capacity
								&& !student.ClassSchedule.Select(c => c.Period).Contains(course.Period))
							{
								course.Students.Add(student);
								student.ClassSchedule.Add(course);
								score += preferenceIndex;
								break;
							}
						}
					}
				}
			}

			var lowestScore = Enumerable.Range(0, periods).Sum() * students.Count();
			Console.WriteLine($"Preference misses: {score-lowestScore}");

			foreach (var student in students.OrderBy(s => s.Priority))
			{
				student.ClassSchedule.Sort((c1, c2) => c1.Period - c2.Period);

				if (student.ClassSchedule.Count < periods)
				{
					Console.WriteLine($"Student {student.Name} only has {student.ClassSchedule.Count}/{periods} classes");
					//throw new Exception($"Student {student.Name} only has {student.ClassSchedule.Count}/{periods} classes");
				}
			}
		}

		static void WriteStudentSchedules(Student[] students)
		{
			// name,p1,p2,p3,p4
			var content = string.Join("\r\n", students.OrderBy(s => s.Name).Select(s => $"{s.Name},{string.Join(",", s.ClassSchedule.OrderBy(c => c.Period).Select(c => c.Name))}"));
			File.WriteAllText(ByStudent, content);

			content = string.Join("\r\n\r\n", students.OrderBy(s => s.Name).Select(s => $"{s.Name}\r\n{new string('-', s.Name.Length)}\r\n{string.Join("\r\n", s.ClassSchedule.OrderBy(c => c.Period).Select((s, i) => $"{s.Period}: {s.Name}"))}"));
			File.WriteAllText(ByStudentPrintable, content);
		}

		static void WriteClassSchedules(Course[] courses)
		{
			var content = string.Join("\r\n" + MSWordPageBreak,
				courses.GroupBy(c => c.Teacher)
					.OrderBy(g => g.Key)
					.Select(t => string.Join("\r\n\r\n", t.OrderBy(c => c.Period).Select(c => $"### p{c.Period} {c.Name} ({c.Teacher} @ {c.Room})\r\n{string.Join("\r\n", c.Students.OrderBy(s => s.Name).Select((s, i) => $"{i+1}. {s.Name}"))}"))));
			File.WriteAllText(RasterByClassPrintable, content);

			var pad = courses.Max(c => $"p{c.Period} {c.Name}".Length + 2);
			content = string.Join("\r\n", courses.GroupBy(c => c.Teacher).OrderBy(g => g.Key).Select(t => $"### {t.Key}\r\n" + string.Join("\r\n", t.OrderBy(c => c.Period).Select(c => $"p{c.Period} {c.Name} {new string(' ', Math.Max(pad - $"p{c.Period} {c.Name} ".Length, 1))} Size: {c.Students.Count}/{c.Capacity} in {c.Room}"))));
			content = $"{courses.SelectMany(c => c.Students).Distinct().Count()} students across {courses.Count()} class periods.\r\n{content}";
			File.WriteAllText(RasterByTeacherSummary, content);

			Console.WriteLine(content);
		}

		static void WriteSurvey(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			Console.Out.WriteLine("Writing Survey.txt");

			var courseNames = courses.Select(c => c.Name).Distinct().ToArray();

			StringBuilder content = new StringBuilder();

			foreach (var student in students)
			{
				content.AppendLine($"{student.Name} ({student.Family})");
				content.AppendLine($"Rank 1 through {courseNames.Length} next to each (1 is the best)");
				content.AppendLine();

				foreach (var course in courseNames)
				{
					content.AppendLine($"____  {course}");
					content.AppendLine();
				}

				content.Append(MSWordPageBreak);
			}

			File.WriteAllText(SurveyFilePath, content.ToString());
		}
	}
}

