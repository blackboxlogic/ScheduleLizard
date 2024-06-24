using System;
using System.Collections.Generic;
using System.Linq;

namespace ScheduleLizard
{
	class Student
	{
		public string Name;
		public string Location;
		public int Priority;
		// Course Names
		public string[] CoursePreferencesInOrder;
		public string[] PastTakenClasses;
		public List<Course> ClassSchedule = new List<Course>();

		public override string ToString()
		{
			return $"{Name} ({Location}, {string.Join(", ", ClassSchedule.OrderBy(c => c.Period).Select(c => c.Period + "." + c.Name))}";
		}
	}
}
