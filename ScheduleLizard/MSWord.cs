using System;
using System.IO;
using Microsoft.Office.Interop.Word;

namespace ScheduleLizard
{
	//https://stackoverflow.com/questions/44888038/create-insert-text-and-save-a-word-doc-in-c-sharp
	static class MSWord
	{
		public static void WriteDocX(string filePath, string content)
		{
			Application app = null;
			Document doc = null;
			try
			{
				app = new Application();
				doc = app.Documents.Add();
				Selection currentSelection = app.Selection;
				currentSelection.TypeText(content);
				currentSelection.PageSetup.TopMargin *= (float).9;
				currentSelection.PageSetup.BottomMargin *= (float).9;
				doc.Content.Paragraphs.SpaceAfter = 0;

				//https://stackoverflow.com/questions/48170215/page-columns-in-interop-word
				//Selection.InsertBreak Type:=wdSectionBreakContinuous
				//doc.Content.Paragraphs

				doc.SaveAs2(Path.Combine(Environment.CurrentDirectory, filePath));
			}
			finally
			{
				doc?.Close();
				app?.Quit();
			}
		}

		public static void WritePDF(string filePath, string content)
		{
			Application app = null;
			Document doc = null;
			try
			{
				app = new Application();
				doc = app.Documents.Add();
				Selection currentSelection = app.Selection;
				currentSelection.TypeText(content);
				currentSelection.PageSetup.TopMargin *= (float).9;
				currentSelection.PageSetup.BottomMargin *= (float).9;
				doc.Content.Paragraphs.SpaceAfter = 0;
				doc.ExportAsFixedFormat(Path.Combine(Environment.CurrentDirectory, filePath), WdExportFormat.wdExportFormatPDF);
			}
			finally
			{
				doc?.Close();
				app?.Quit();
			}
		}
	}
}
