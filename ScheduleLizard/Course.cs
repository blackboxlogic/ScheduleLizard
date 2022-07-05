using System;
using System.Collections.Generic;

namespace ScheduleLizard
{
	class Course
	{
		public string Name;
		public string Teacher;
		public string Room;
		public int Capacity;
		public int Period;
		public readonly List<Student> Students = new List<Student>();

		public override string ToString()
		{
			return $"{Name}({Period})";
		}
	}
}
