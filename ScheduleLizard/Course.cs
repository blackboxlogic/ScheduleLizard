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
		public int? MinCapacity;
		public int Capacity;
		public string Periods; // like "1;2;4"
		public int Period;
		public bool CanRetake;
		public string Topic; // to say that two classes might be different levels of the same topic and kids shouldn't be in both of them
		public int? Level;
		public string Helpers;
		public List<Student> Students = new List<Student>();

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
			clone.Students = new List<Student>();
			return clone;
		}

		public override string ToString()
		{
			return $"{Name} ({Teacher}, p{Period}, {Students.Count}/{Capacity}, with {Helpers})";
		}
	}
}
