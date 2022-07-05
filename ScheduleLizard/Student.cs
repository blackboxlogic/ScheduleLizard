using System;
using System.Collections.Generic;

namespace ScheduleLizard
{
	class Student
	{
		public string Name;
		public int Priority;
		public string[] CoursePreferencesInOrder;
		public List<Course> ClassSchedule = new List<Course>();
	}
}
