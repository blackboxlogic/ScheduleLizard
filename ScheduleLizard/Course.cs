using System;
using System.Collections.Generic;

namespace ScheduleLizard
{
	class Course
	{
		public string Name;
		public string Teacher;
		public string Location;
		public int Capacity;
		public int Period;
		public bool CanRetake;
		public string topic; // to say that two classes might be different levels of the same topic and kids shouldn't be in both of them
		public readonly List<Student> Students = new List<Student>();

		public override string ToString()
		{
			return $"{Name} ({Teacher}, p{Period}, {Students.Count}/{Capacity})";
		}
	}
}
