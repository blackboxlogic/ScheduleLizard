using System;
using System.IO;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleLizard
{
	class Program
	{
		const string OutputPath = "Output";
		const int RandomSeed = 100;
		const char MSWordPageBreak = '\f';

		static void Main(string[] args)
		{
			try
			{
				var courses = InputCourses().ToArray();
				var students = InputStudents().ToArray();
				Validate(courses, students);
				CalculateSchedule(courses, students);
				CleanOutputFolder();
				Output(students);
				Output(courses);
			}
			catch (Exception e)
			{
				Console.Error.WriteLine(e);
			}

			Console.ReadKey(true);
		}

		static void CleanOutputFolder()
		{
			if (Directory.Exists(OutputPath))
			{
				Directory.Delete(OutputPath, true);
			}

			Directory.CreateDirectory(OutputPath);
		}

		static Course[] InputCourses()
		{
			var lines = File.ReadAllLines(@"Course Schedule.csv")
				.Skip(1) // ignore header line
				.Where(l => !l.StartsWith("//")) // Allow comment lines
				.Select(l => l.Split(','))
				.ToArray();
			var schedule = lines.Select(l => new Course() { Name = l[0], Capacity = int.Parse(l[1]), Period = int.Parse(l[2]), Teacher = l[3], Room = l[4] }).ToArray();

			return schedule;
		}

		static IEnumerable<Student> InputStudents()
		{
			var lines = File.ReadAllLines(@"Camper Preferences.csv")
				.Where(l => !l.StartsWith("//")) // Allow comment lines
				.Select(l =>l.Split(','))
				.ToArray();
			var courses = lines[0].Skip(2).ToArray();
			var studentLines = lines.Skip(1).OrderBy(s => DateTime.Parse(s[0])).ToArray();
			var priority = 0;

			foreach (string[] studentLine in studentLines)
			{
				var preferences = studentLine
					.Skip(2) // date and name
					.Select(rank =>
						rank == "NO" || rank == "0" ? 999 :
						rank == "" ? 50 :
						int.Parse(rank))
					.Zip(courses, (p, c) => new { p, c })
					.Shuffle(RandomSeed)
					.OrderBy(z => z.p)
					.Select(z => z.c)
					.ToArray();
				var student = new Student() { Name = studentLine[1], Priority = priority++, CoursePreferencesInOrder = preferences };
				yield return student;
			}
		}

		static void Validate(IEnumerable<Course> courses, IEnumerable<Student> students)
		{
			// There's enough capacity at each period
			for (int i = 1; i < 5; i++)
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

			foreach (var student in students.OrderBy(s => s.Priority))
			{
				foreach (var preference in student.CoursePreferencesInOrder)
				{
					if (student.ClassSchedule.Count < periods)
					{
						foreach (var course in courses.OrderBy(c => c.Students.Count))
						{
							if (course.Name == preference
								&& course.Students.Count < course.Capacity
								&& !student.ClassSchedule.Select(c => c.Period).Contains(course.Period))
							{
								course.Students.Add(student);
								student.ClassSchedule.Add(course);
								break;
							}
						}
					}
				}

				student.ClassSchedule.Sort((c1, c2) => c1.Period - c2.Period);

				if (student.ClassSchedule.Count < periods)
				{
					// Maybe try again?
					throw new Exception($"Student {student.Name} only has {student.ClassSchedule.Count}/{periods} classes");
				}
			}
		}

		static void Output(Student[] students)
		{
			// name,p1,p2,p3,p4
			var content = string.Join("\n", students.OrderBy(s => s.Name).Select(s => $"{s.Name},{string.Join(",", s.ClassSchedule.OrderBy(c => c.Period).Select(c => c.Name))}"));
			var path = Path.Combine(OutputPath, $"ByStudent.csv");
			File.WriteAllText(path, content);

			content = string.Join("\n\n", students.OrderBy(s => s.Name).Select(s => $"{s.Name}\n{new string('-', s.Name.Length)}\n{string.Join('\n', s.ClassSchedule.OrderBy(c => c.Period).Select((s, i) => $"{s.Period}: {s.Name}"))}"));
			path = Path.Combine(OutputPath, $"ByStudentPrintable.txt");
			File.WriteAllText(path, content);
		}

		static void Output(Course[] courses)
		{
			var content = string.Join("\n", courses.OrderBy(c => c.Teacher).ThenBy(c => c.Period).Select(c => $"p{c.Period} {c.Name} ({c.Teacher} @ {c.Room})\n------------\n{string.Join('\n', c.Students.OrderBy(s => s.Name).Select((s, i) => $"{i}. {s.Name}"))}\n{MSWordPageBreak}"));
			var path = Path.Combine(OutputPath, $"ByClassPrintable.txt");
			File.WriteAllText(path, content);

			var pad = courses.Max(c => $"p{c.Period} {c.Name}".Length + 2);
			content = string.Join("\n", courses.OrderBy(c => c.Teacher).ThenBy(c => c.Period).Select(c => $"p{c.Period} {c.Name} {new string(' ', Math.Max(pad - $"p{c.Period} {c.Name} ".Length, 1))} Size: {c.Students.Count}/{c.Capacity} ({c.Teacher} in {c.Room})"));
			content = $"{courses.SelectMany(c => c.Students).Distinct().Count()} students across {courses.Count()} class periods.\n{content}";
			path = Path.Combine(OutputPath, $"ByTeacherSummary.txt");
			File.WriteAllText(path, content);

			Console.WriteLine(content);
		}
	}
}

