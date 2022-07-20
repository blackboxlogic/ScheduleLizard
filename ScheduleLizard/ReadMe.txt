INPUT
There needs to be a file named "Camper Preferences.csv" in the same folder
	- CHECK FOR DUPLICATE NAMES
	- CHECK FOR MISSING NAMES
	- must have a header line
	- must have columns: signUpDate, camperName, class1Preference...
	- can comment out lines by starting with //
	- preferences can be a number (lower number is higher priority), can be empty, can be "NO"
There needs to be a file named "Course Schedule.csv"in the same folder.
	- class names must match the headers in "Camper Preferences.csv"
	- must have a header line
	- must have columns: Name,Capacity,Period,Teacher,Room
	- can comment out lines by starting with //

OUTPUT
Print "ByClassPrintable.txt" using word
Print "ByStudent.csv" using Excel
	- Auto-width columns
	- Narrow margins
Print "ByStudentPrintable.txt" using word
	- make it two columns
	- adjust page height to fit whole kids
Print "ByTeacherSummary.txt" using word