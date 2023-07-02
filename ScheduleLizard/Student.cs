using System;
using System.Collections.Generic;

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
	}
}
