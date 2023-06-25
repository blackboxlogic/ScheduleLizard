using System;
using System.Collections.Generic;

namespace ScheduleLizard
{
	class Student
	{
		public string Name;
		public string Family;
		public int Priority;
		// Course Names
		public string[] CoursePreferencesInOrder;
		public List<Course> ClassSchedule = new List<Course>();
	}
}
