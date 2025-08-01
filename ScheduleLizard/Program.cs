﻿using System;
using System.IO;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace ScheduleLizard
{
	// Update StudentList
	// Update CoursDefinitions
	// Print surveys (1), convert to pdf, print (get pencils)
	// Students fill out surveys
	// Update StudentPreferences
	// calculate schedules (2), convert to pdf, print signs, class roster, student schedules (cut up), give to family mentors
	// Each teacher gets a class sign and class raster

	// TODO: Read/Write to google drive? A database? A static website? PDF?
	// TODO: Format columns to roster and student schedules output.
	class Program
	{
		const string StudentListFile = @"Input\StudentList.csv";
		const string CourseScheduleFile = @"Input\CourseDefinitions.csv";
		const string StudentPreferenceFile = @"Input\StudentPreferences.csv";
		// A list of campers and the classes they've taken previously to prevent re-takes.
		// <studentName>,<className>,<className>... (No header line)
		const string ByStudentOLD = @"Input\OldClassesByStudent.csv";

		const string SurveyFilePath = @"Output\StudentCourseSurveyPrintable.txt";
		const string StudentPreferenceTemplateFile = @"Output\StudentPreferencesTemplate.csv";
		const string ByStudentGrid = @"Output\Student Schedules.csv";
		const string ClassRosters = @"Output\Classes Rosters.txt";
		const string TeacherSchedules = @"Output\Teacher Schedules.txt";
		const string StudentSchedules = @"Output\Student Schedules.txt";

		const int RandomSeed = 100; // Deterministic output
		const char MSWordPageBreak = '\f';
		const bool PackTightly = false; // vs load balance
		const bool PrioritizeNewStudents = true;
		const int DefaultPreference = 50;
		const int NumberOfPeriods = 3;
		const bool TwoSurveyPerPage = true;

		static void Main(string[] args)
		{
			try
			{
				while(true)
				{
					Console.WriteLine("1) Generate Survey\n2) calculate/print Schedule\n3) re-print schedules\n4) exit");
					var key = Console.ReadKey().KeyChar;
					Console.WriteLine();

					if (key == '1')
					{
						var courses = InputCourses();
						var students = InputStudents();
						WriteSurvey(courses, students);
						WritePreferenceTemplate(courses, students);
					}
					else if (key == '2')
					{
						var courses = InputCourses();
						var studentPreferences = InputStudentsWithPreferences().ToArray();
						Validate(courses, studentPreferences);
						SummarizeCoursePopularity(courses, studentPreferences);
						CalculateSchedule(courses, studentPreferences);
						WriteStudentSchedules(studentPreferences);
						WritePrintable(courses, studentPreferences);
					}
					else if (key == '3')
					{
						var courses = InputCourses();
						var studentSchedules = ReadStudentSchedules(courses).ToArray();
						WritePrintable(courses, studentSchedules);
					}
					else if (key == '4')
					{
						return;
					}
				}
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
				Console.ReadKey();
			}
		}

		static Course[] InputCourses()
		{
			var lines = File.ReadAllLines(CourseScheduleFile)
				.Skip(1) // ignore header line
				.Where(l => !l.StartsWith("//")) // Allow comment lines
				.Select(l => l.Split(','))
				.ToArray();
			var schedule = lines.Select(l => new Course() {
				Name = l[0],
				MinCapacity = l[1] == "" ? (int?) null : int.Parse(l[1]),
				Capacity = int.Parse(l[2]),
				Periods = l[3],
				Teacher = l[4],
				Location = l[5],
				CanRetake = bool.Parse(l[6]),
				Topic = l[7],
				Level = l[8] == "" ? (int?)null : int.Parse(l[8]),
				Helpers = l[9]})
				.SelectMany(c => c.AsPeriods()) // break into periods
				.ToArray();

			return schedule;
		}

		static Student[] InputStudents()
		{
			return File.ReadAllLines(StudentListFile)
				.Skip(1) // ignore header line
				.Where(l => !l.StartsWith("//")) // Skip comment lines
				.Select(l => l.Split(','))
				.Select(n => new Student() { Name = n[0], Location = n[1] })
				.ToArray();
		}

		static IEnumerable<Student> ReadStudentSchedules(Course[] courses)
		{
			var pastClasses = File.ReadAllLines(ByStudentGrid)
				.Where(l => !l.StartsWith("//"))
				.Select(l => l.Split(","))
				.ToArray();
			foreach (var record in pastClasses)
			{
				var student = new Student()
				{
					Name = record[0],
					Location = record[1]
				};

				for (int i = 1; i <= NumberOfPeriods; i++)
				{
					if (record[1 + i] == "")
					{
						Console.WriteLine($"{student.Name} ({student.Location}) doesn't have a period {i} class. Looked for class named '{record[1 + i]}'");
					}
					else
					{
						var course = courses.Single(c => c.Period == i && c.Name == record[1 + i]);
						student.ClassSchedule.Add(course);
						course.Students.Add(student);
					}
				}

				yield return student;
			}
		}

		static void WritePreferenceTemplate(Course[] courses, Student[] students)
		{
			var content = new StringBuilder();
			content.AppendLine(string.Join(",", courses.Select(c => c.Name).Distinct().Prepend("location").Prepend("priority").Prepend("studentName")));

			foreach (var student in students.OrderBy(s => s.Location).ThenBy(s => s.Name))
			{
				content.AppendLine(string.Join(",", student.Name, 0, student.Location));
			}

			File.WriteAllText(StudentPreferenceTemplateFile, content.ToString());
		}

		static IEnumerable<Student> InputStudentsWithPreferences()
		{
			var lines = File.ReadAllLines(StudentPreferenceFile)
				.Where(l => !l.StartsWith("//")) // Skip comment lines
				.Select(l =>l.Split(','))
				.ToArray();
			var courses = lines[0].Skip(3).ToArray();
			var studentLines = lines.Skip(1).OrderBy(s => int.Parse(s[1])).ToArray();
			var pastClasses = new Dictionary<string, string[]>();

			if (File.Exists(ByStudentOLD))
			{
				pastClasses = File.ReadAllLines(ByStudentOLD)
					.Select(l => l.Split(","))
					.ToDictionary(l => l[0], l => l.Skip(1).ToArray());
			}
			else
			{
				Console.WriteLine("Warning: no past class list found.");
			}

			foreach (string[] studentLine in studentLines)
			{
				var name = studentLine[0];
				var priority = int.Parse(studentLine[1]);
				var family = studentLine[2];
				string[] preferences;

				if (studentLine.Length != courses.Length + 3)
				{
					Console.WriteLine($"WARNING: Student {name} has the wrong number of course preferences");
					preferences = new string[0];
				}
				else
				{
					preferences = studentLine
						.Skip(3) // Skip priority, name, family
						.Select(rank => rank == "" ? DefaultPreference : int.Parse(rank))
						.Concat(Enumerable.Repeat(1, courses.Length))
						.Zip(courses, (p, c) => new { p, c })
						.Shuffle(RandomSeed)
						.OrderBy(z => z.p)
						.Select(z => z.c)
						.ToArray();
				}

				var student = new Student()
				{
					Name = name,
					Priority = priority,
					Location = family,
					CoursePreferencesInOrder = preferences
				};

				if (pastClasses.ContainsKey(name))
				{
					student.PastTakenClasses = pastClasses[name];

					// Deprioritize two-weekers
					if (PrioritizeNewStudents)
					{
						student.Priority -= 1;
					}

					Console.WriteLine($"Detected returning student: {name}");
				}
				else
				{
					student.PastTakenClasses = new string[0];
				}

				yield return student;
			}
		}

		static void SummarizeCoursePopularity(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			var classPopularity = students
				.SelectMany(s => s.CoursePreferencesInOrder.Take(4))
				.GroupBy(c => c)
				.ToDictionary(c => c.Key, c => c.Count());

			var coursesByName = courses.GroupBy(c => c.Name).ToDictionary(g => g.Key, g => g.ToArray());

			Console.WriteLine($"Popularity by class");
			foreach (var course in classPopularity.OrderByDescending(p => p.Value))
			{
				Console.WriteLine($"{course.Key}: {course.Value}");
			}

			Console.WriteLine($"Popularity by period");
			foreach (var period in courses.GroupBy(c => c.Period).OrderBy(g => g.Key))
			{
				var popularity = period.Sum(c => classPopularity[c.Name]);
				Console.WriteLine($"P{period.Key}: {popularity}");
			}

			Console.WriteLine($"Available class slots by period");
			foreach (var period in courses.GroupBy(c => c.Period).OrderBy(g => g.Key))
			{
				var slots = period.Sum(c => c.Capacity);
				Console.WriteLine($"P{period.Key}: {slots}");
			}
		}

		static void Validate(Course[] courses, Student[] students)
		{
			if (students.SelectMany(s => s.ClassSchedule).Any(c => c.Name == ""))
			{
				throw new Exception("Student got assigned an empty course?");
			}

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
				Console.WriteLine("Warning: " + $"{string.Join(", ", missingClasses)} classes were requested but are not offered");
			}

			// Only one class per period per teacher
			var overBookedTeachers = courses.GroupBy(c => new { c.Period, c.Teacher }).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
			foreach (var overTeacher in overBookedTeachers)
			{
				throw new Exception($"{overTeacher.Teacher} is overbook in period {overTeacher.Period}.");
			}

			// Only one class per period per room
			var overBookedRooms = courses.GroupBy(c => new { c.Period, c.Location }).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
			foreach (var overRoom in overBookedRooms)
			{
				throw new Exception($"{overRoom.Location} is overbook in period {overRoom.Period}.");
			}
		}

		static void CalculateSchedule(Course[] courses, Student[] students)
		{
			var periods = courses.Max(c => c.Period);
			var courseCount = courses.GroupBy(c => c.Name).Count();
			var qualityControl = new Dictionary<int, int>();

			var coursesOrdered = PackTightly
				? courses.OrderByDescending(c => c.Students.Count)
				: courses.OrderBy(c => c.Students.Count);

			students = students.Shuffle(RandomSeed).ToArray();

			for (var preferenceIndex = 0; preferenceIndex < courseCount; preferenceIndex++)
			{
				qualityControl[preferenceIndex] = 0;

				foreach (var student in students.OrderByDescending(s => s.Priority))
				{
					var prefered = student.CoursePreferencesInOrder[preferenceIndex];

					if (student.ClassSchedule.Count < periods)
					{
						foreach (var course in coursesOrdered)
						{
							if (course.Name == prefered
								// class isn't full
								&& course.Students.Count < course.Capacity
								// student is available for this class period
								&& !student.ClassSchedule.Select(c => c.Period).Contains(course.Period)
								// class can be retaken or student hasn't taken it before
								&& (course.CanRetake || !student.PastTakenClasses.Contains(course.Name))
								// student hasn't taken a higher level class of the same topic
								//&& (!courses.Where(c => student.PastTakenClasses.Contains(c.Name)).Any(c => c.topic == course.topic && c.level <= course.level))
								// student isn't taking a class with the same topic
								&& !student.ClassSchedule.Any(c => c.Topic != null && c.Topic != "" && c.Topic == course.Topic))
							{
								course.Students.Add(student);
								student.ClassSchedule.Add(course);
								qualityControl[preferenceIndex] = qualityControl[preferenceIndex] + 1;
								break;
							}
						}
					}
				}

				students = students.Reverse().ToArray();
			}

			var lowestScore = Enumerable.Range(0, periods).Sum() * students.Count();

			foreach (var q in qualityControl.Where(q => q.Value != 0).OrderBy(q => q.Key))
			{
				Console.WriteLine($"{q.Key+ 1} choice matches: {q.Value}/{students.Count()}");
			}

			foreach (var student in students.OrderBy(s => s.Priority))
			{
				student.ClassSchedule.Sort((c1, c2) => c1.Period - c2.Period);

				if (student.ClassSchedule.Count < periods)
				{
					Console.WriteLine($"Student {student.Name} only has {student.ClassSchedule.Count}/{periods} classes");
				}
			}

			var duplicateTopic = students.Where(s => s.ClassSchedule.Where(c => c.Topic != null && c.Topic != "").GroupBy(c => c.Topic).Any(g => g.Count() > 1)).ToArray();
			if (duplicateTopic.Any())
			{
				Console.WriteLine("Warning, Kids have duplicate topics");
			}

			var smallClasses = courses.Where(c => c.Students.Count < c.MinCapacity).ToArray();
			foreach(var smallClass in smallClasses)
			{
				Console.WriteLine($"Warning, {smallClass.Name} has fewer than {smallClass.MinCapacity} students.");
			}
		}

		static void WriteStudentSchedules(Student[] students)
		{
			// name,location,p1,p2,p3,p4
			var content = string.Join("\r\n", students.OrderBy(s => s.Name).Select(s => $"{s.Name},{s.Location},{string.Join(",", s.ClassSchedule.OrderBy(c => c.Period).Select(c => c.Name))}"));
			File.WriteAllText(ByStudentGrid, content);
		}

		static void WritePrintable(Course[] courses, Student[] students)
		{
			var content = string.Join("\r\n\r\n", students.OrderBy(s => s.Location).ThenBy(s => s.Name).Select(s => $"{s.Name} ({s.Location})\r\n{new string('-', s.Name.Length)}\r\n{string.Join("\r\n", s.ClassSchedule.OrderBy(c => c.Period).Select((s, i) => $"{s.Period}: {s.Name}"))}"));
			// TODO: change, the margins, make 2-column, save as PDF.
			//MSWord.WritePDF(ClassRosters.Replace(".txt", ".pdf"), content, p => 1, (float).9);
			File.WriteAllText(StudentSchedules, content);

			content = string.Join("\r\n" + MSWordPageBreak,
				courses.GroupBy(c => c.Teacher)
					.OrderBy(g => g.Key)
					.Select(t => string.Join("\r\n\r\n", t.OrderBy(c => c.Period).Select(c => $"### p{c.Period} {c.Name} ({c.Teacher} @ {c.Location} w/ {c.Helpers})\r\n{string.Join("\r\n", c.Students.OrderBy(s => s.Name).Select((s, i) => $"{i+1}. {s.Name}"))}"))));
			File.WriteAllText(ClassRosters, content);
			//MSWord.WriteDocX(ClassRosters.Replace(".txt", ".docx"), content);

			var pad = courses.Max(c => $"p{c.Period} {c.Name}".Length + 2);
			content = string.Join("\r\n", courses.GroupBy(c => c.Teacher).OrderBy(g => g.Key).Select(t => $"### {t.Key}\r\n" + string.Join("\r\n", t.OrderBy(c => c.Period).Select(c => $"p{c.Period} {c.Name} {new string(' ', Math.Max(pad - $"p{c.Period} {c.Name} ".Length, 1))} Size: {c.Students.Count}/{c.Capacity} in {c.Location}"))));
			content = $"{courses.SelectMany(c => c.Students).Distinct().Count()} students across {courses.Count()} class periods.\r\n{content}";
			File.WriteAllText(TeacherSchedules, content);

			Console.WriteLine(content);
		}

		static void WriteSurvey(Course[] courses, Student[] students)
		{
			Console.Out.WriteLine($"Writing {SurveyFilePath}");

			var distinctCourses = courses.GroupBy(c => c.Name).Select(g => g.First()).ToArray();

			StringBuilder content = new StringBuilder();

			int i = 0;

			foreach (var student in students
				// Add some extra nameless surveys
				.Concat(Enumerable.Repeat(new Student() { Name = "Name:__________________________", Location = "___________" }, (int)(students.Length * .07)))
				.OrderBy(s => s.Location)
				.ThenBy(s => s.Name))
			{
				content.AppendLine($"{student.Name} ({student.Location} family)");
				content.AppendLine($"Write 1 on your favorite class, 2 on your second favorite... up to {Math.Min(9, distinctCourses.Length)}.");
				content.AppendLine();

				foreach (var course in distinctCourses)
				{
					content.AppendLine($"_____  {course.Name} ({course.Teacher})");
					content.AppendLine();
				}

				if(i % 2 == 1 || !TwoSurveyPerPage)
				{
					content.Append(MSWordPageBreak);
				}
				else
				{
					for (int j = 0; j < 14; j++) {
						content.AppendLine();
					}
				}

				i++;
			}

			File.WriteAllText(SurveyFilePath, content.ToString().TrimEnd(MSWordPageBreak));
		}
	}
}

