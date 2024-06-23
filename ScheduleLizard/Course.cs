using System;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleLizard
{
	class Course
	{
		public string Name;
		public string Teacher;
		public string Location;
		public int Capacity;
		public string Periods; // like "124"
		public int Period;
		public bool CanRetake;
		public string topic; // to say that two classes might be different levels of the same topic and kids shouldn't be in both of them
		public readonly List<Student> Students = new List<Student>();

		public IEnumerable<Course> AsPeriods()
		{
			return Periods
				.Where(char.IsNumber)
				.Select(p => ForPeriod((int)char.GetNumericValue(p)));
		}

		private Course ForPeriod(int p)
		{
			var clone = MemberwiseClone() as Course;
			clone.Period = p;
			clone.Periods = null;
			return clone;
		}

		public override string ToString()
		{
			return $"{Name} ({Teacher}, p{Period}, {Students.Count}/{Capacity})";
		}
	}
}
