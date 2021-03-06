﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.Forms;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class SplitLongLines : Form
    {

        private Subtitle _subtitle;
        private Subtitle _splittedSubtitle;

        public int NumberOfSplits { get; private set; }

        public Subtitle SplittedSubtitle
        {
            get { return _splittedSubtitle; }
        }

        public SplitLongLines()
        {
            InitializeComponent();
            FixLargeFonts();
        }

        private void FixLargeFonts()
        {
            Graphics graphics = this.CreateGraphics();
            SizeF textSize = graphics.MeasureString(buttonOK.Text, this.Font);
            if (textSize.Height > buttonOK.Height - 4)
            {
                int newButtonHeight = (int)(textSize.Height + 7 + 0.5);
                Utilities.SetButtonHeight(this, newButtonHeight, 1);
            }
        }

        private void SplitLongLines_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
                DialogResult = DialogResult.Cancel;
        }

        public void Initialize(Subtitle subtitle)
        {
            if (subtitle.Paragraphs.Count > 0)
                subtitle.Renumber(subtitle.Paragraphs[0].Number);

            Text = Configuration.Settings.Language.SplitLongLines.Title;
            labelSingleLineMaxLength.Text = Configuration.Settings.Language.SplitLongLines.SingleLineMaximumLength;
            labelLineMaxLength.Text = Configuration.Settings.Language.SplitLongLines.LineMaximumLength;
            labelLineContinuationBeginEnd.Text = Configuration.Settings.Language.SplitLongLines.LineContinuationBeginEndStrings;

            listViewFixes.Columns[0].Text = Configuration.Settings.Language.General.Apply;
            listViewFixes.Columns[1].Text = Configuration.Settings.Language.General.LineNumber;
            listViewFixes.Columns[2].Text = Configuration.Settings.Language.General.Text;

            buttonOK.Text = Configuration.Settings.Language.General.Ok;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            SubtitleListview1.InitializeLanguage(Configuration.Settings.Language.General, Configuration.Settings);
            Utilities.InitializeSubtitleFont(SubtitleListview1);
            SubtitleListview1.AutoSizeAllColumns(this);
            NumberOfSplits = 0;
            numericUpDownSingleLineMaxCharacters.Value = Configuration.Settings.General.SubtitleLineMaximumLength;
            _subtitle = subtitle;
        }

        private void AddToListView(Paragraph p, string lineNumbers, string newText)
        {
            var item = new ListViewItem(string.Empty) { Tag = p, Checked = true };

            var subItem = new ListViewItem.ListViewSubItem(item, lineNumbers);
            item.SubItems.Add(subItem);
            subItem = new ListViewItem.ListViewSubItem(item, newText.Replace(Environment.NewLine, Configuration.Settings.General.ListViewLineSeparatorString));
            item.SubItems.Add(subItem);

            listViewFixes.Items.Add(item);
        }

        private void listViewFixes_ItemChecked(object sender, ItemCheckedEventArgs e)
        {
            GeneratePreview(false);
        }

        private void GeneratePreview(bool clearFixes)
        {
            if (_subtitle == null)
                return;

            var splittedIndexes = new List<int>();
            var autoBreakedIndexes = new List<int>();

            NumberOfSplits = 0;
            SubtitleListview1.Items.Clear();
            SubtitleListview1.BeginUpdate();
            int count;
            _splittedSubtitle = SplitLongLinesInSubtitle(_subtitle, splittedIndexes, autoBreakedIndexes, out count, (int)numericUpDownLineMaxCharacters.Value, (int)numericUpDownSingleLineMaxCharacters.Value, clearFixes);
            NumberOfSplits = count;

            SubtitleListview1.Fill(_splittedSubtitle);

            foreach (var index in splittedIndexes)
                SubtitleListview1.SetBackgroundColor(index, Color.Green);

            foreach (var index in autoBreakedIndexes)
                SubtitleListview1.SetBackgroundColor(index, Color.LightGreen);

            SubtitleListview1.EndUpdate();
            groupBoxLinesFound.Text = string.Format(Configuration.Settings.Language.SplitLongLines.NumberOfSplits, NumberOfSplits);
            UpdateLongestLinesInfo(_splittedSubtitle);
        }

        private void UpdateLongestLinesInfo(Subtitle subtitle)
        {
            int maxLength = -1;
            int maxLengthIndex = -1;
            int singleLineMaxLength = -1;
            int singleLineMaxLengthIndex = -1;
            int i = 0;
            foreach (Paragraph p in subtitle.Paragraphs)
            {
                string s = Utilities.RemoveHtmlTags(p.Text);
                if (s.Length > maxLength)
                {
                    maxLength = s.Length;
                    maxLengthIndex = i;
                }
                string[] arr = s.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                foreach (string line in arr)
                {
                    if (line.Length > singleLineMaxLengthIndex)
                    {
                        singleLineMaxLength = line.Length;
                        singleLineMaxLengthIndex = i;
                    }
                }
                i++;
            }
            labelMaxSingleLineLengthIs.Text = string.Format(Configuration.Settings.Language.SplitLongLines.LongestSingleLineIsXAtY, singleLineMaxLength, singleLineMaxLengthIndex + 1);
            labelMaxSingleLineLengthIs.Tag = singleLineMaxLengthIndex.ToString();
            labelMaxLineLengthIs.Text = string.Format(Configuration.Settings.Language.SplitLongLines.LongestLineIsXAtY, maxLength, maxLengthIndex + 1);
            labelMaxLineLengthIs.Tag = maxLengthIndex.ToString();
        }

        private bool IsFixAllowed(Paragraph p)
        {
            foreach (ListViewItem item in listViewFixes.Items)
            {
                if ((item.Tag as Paragraph) == p)
                    return item.Checked;
            }
            return true;
        }

        public Subtitle SplitLongLinesInSubtitle(Subtitle subtitle, List<int> splittedIndexes, List<int> autoBreakedIndexes, out int numberOfSplits, int totalLineMaxCharacters, int singleLineMaxCharacters, bool clearFixes)
        {
            listViewFixes.ItemChecked -= listViewFixes_ItemChecked;
            if (clearFixes)
                listViewFixes.Items.Clear();
            numberOfSplits = 0;
            string language = Utilities.AutoDetectGoogleLanguage(subtitle);
            Subtitle splittedSubtitle = new Subtitle();
            Paragraph p = null;
            for (int i = 0; i < subtitle.Paragraphs.Count; i++)
            {
                bool added = false;
                p = subtitle.GetParagraphOrDefault(i);
                if (p != null && p.Text != null)
                {
                    string oldText = Utilities.RemoveHtmlTags(p.Text);
                    if (SplitLongLinesHelper.QualifiesForSplit(p.Text, singleLineMaxCharacters, totalLineMaxCharacters) && IsFixAllowed(p))
                    {
                        bool isDialog = false;
                        string dialogText = string.Empty;
                        if (p.Text.Contains('-'))
                        {
                            dialogText = Utilities.AutoBreakLine(p.Text, 5, 1, language);
                            string[] arr = dialogText.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                            if (arr.Length == 2 && (arr[0].StartsWith('-') || arr[0].StartsWith("<i>-")) && (arr[1].StartsWith('-') || arr[1].StartsWith("<i>-")))
                                isDialog = true;

                        }

                        if (!isDialog && !SplitLongLinesHelper.QualifiesForSplit(Utilities.AutoBreakLine(p.Text, language), singleLineMaxCharacters, totalLineMaxCharacters))
                        {
                            Paragraph newParagraph = new Paragraph(p);
                            newParagraph.Text = Utilities.AutoBreakLine(p.Text, language);
                            if (clearFixes)
                                AddToListView(p, (splittedSubtitle.Paragraphs.Count + 1).ToString(), oldText);
                            autoBreakedIndexes.Add(splittedSubtitle.Paragraphs.Count);
                            splittedSubtitle.Paragraphs.Add(newParagraph);
                            added = true;
                            numberOfSplits++;
                        }
                        else
                        {
                            string text = Utilities.AutoBreakLine(p.Text, language);
                            if (isDialog)
                                text = dialogText;
                            if (text.Contains(Environment.NewLine))
                            {
                                string[] arr = text.Split(Environment.NewLine.ToCharArray(), StringSplitOptions.RemoveEmptyEntries);
                                if (arr.Length == 2)
                                {
                                    int spacing1 = Configuration.Settings.General.MininumMillisecondsBetweenLines / 2;
                                    int spacing2 = Configuration.Settings.General.MininumMillisecondsBetweenLines / 2;
                                    if (Configuration.Settings.General.MininumMillisecondsBetweenLines % 2 == 1)
                                        spacing2++;

                                    Paragraph newParagraph1 = new Paragraph(p);
                                    Paragraph newParagraph2 = new Paragraph(p);
                                    newParagraph1.Text = Utilities.AutoBreakLine(arr[0], language);

                                    double startFactor = 0;
                                    double middle = p.StartTime.TotalMilliseconds + (p.Duration.TotalMilliseconds / 2);
                                    if (Utilities.RemoveHtmlTags(oldText).Trim().Length > 0)
                                    {
                                        startFactor = (double)Utilities.RemoveHtmlTags(newParagraph1.Text).Length / Utilities.RemoveHtmlTags(oldText).Length;
                                        if (startFactor < 0.25)
                                            startFactor = 0.25;
                                        if (startFactor > 0.75)
                                            startFactor = 0.75;
                                        middle = p.StartTime.TotalMilliseconds + (p.Duration.TotalMilliseconds * startFactor);
                                    }

                                    newParagraph1.EndTime.TotalMilliseconds = middle - spacing1;
                                    newParagraph2.Text = Utilities.AutoBreakLine(arr[1], language);
                                    newParagraph2.StartTime.TotalMilliseconds = newParagraph1.EndTime.TotalMilliseconds + spacing2;

                                    if (clearFixes)
                                        AddToListView(p, (splittedSubtitle.Paragraphs.Count + 1).ToString(), oldText);
                                    splittedIndexes.Add(splittedSubtitle.Paragraphs.Count);
                                    splittedIndexes.Add(splittedSubtitle.Paragraphs.Count + 1);

                                    string p1 = Utilities.RemoveHtmlTags(newParagraph1.Text);
                                    if (p1.EndsWith('.') || p1.EndsWith('!') || p1.EndsWith('?') || p1.EndsWith(':') || p1.EndsWith(')') || p1.EndsWith(']') || p1.EndsWith('♪'))
                                    {
                                        if (newParagraph1.Text.StartsWith('-') && newParagraph2.Text.StartsWith('-'))
                                        {
                                            newParagraph1.Text = newParagraph1.Text.Remove(0, 1).Trim();
                                            newParagraph2.Text = newParagraph2.Text.Remove(0, 1).Trim();
                                        }
                                        else if (newParagraph1.Text.StartsWith("<i>-") && newParagraph2.Text.StartsWith('-'))
                                        {
                                            newParagraph1.Text = newParagraph1.Text.Remove(3, 1).Trim();
                                            if (newParagraph1.Text.StartsWith("<i> "))
                                                newParagraph1.Text = newParagraph1.Text.Remove(3, 1).Trim();
                                            newParagraph2.Text = newParagraph2.Text.Remove(0, 1).Trim();
                                        }
                                    }
                                    else
                                    {
                                        bool endsWithComma = newParagraph1.Text.EndsWith(',') || newParagraph1.Text.EndsWith(",</i>");

                                        string post = string.Empty;
                                        if (newParagraph1.Text.EndsWith("</i>"))
                                        {
                                            post = "</i>";
                                            newParagraph1.Text = newParagraph1.Text.Remove(newParagraph1.Text.Length - post.Length);
                                        }
                                        if (endsWithComma)
                                            newParagraph1.Text += post;
                                        else
                                            newParagraph1.Text += comboBoxLineContinuationEnd.Text.TrimEnd() + post;

                                        string pre = string.Empty;
                                        if (newParagraph2.Text.StartsWith("<i>"))
                                        {
                                            pre = "<i>";
                                            newParagraph2.Text = newParagraph2.Text.Remove(0, pre.Length);
                                        }
                                        if (endsWithComma)
                                            newParagraph2.Text = pre + newParagraph2.Text;
                                        else
                                            newParagraph2.Text = pre + comboBoxLineContinuationBegin.Text + newParagraph2.Text;
                                    }

                                    if (newParagraph1.Text.IndexOf("<i>") >= 0 && newParagraph1.Text.IndexOf("<i>") < 10 & newParagraph1.Text.IndexOf("</i>") < 0 &&
                                        newParagraph2.Text.Contains("</i>") && newParagraph2.Text.IndexOf("<i>") < 0)
                                    {
                                        newParagraph1.Text += "</i>";
                                        newParagraph2.Text = "<i>" + newParagraph2.Text;
                                    }

                                    splittedSubtitle.Paragraphs.Add(newParagraph1);
                                    splittedSubtitle.Paragraphs.Add(newParagraph2);
                                    added = true;
                                    numberOfSplits++;
                                }
                            }
                        }
                    }
                }
                if (!added)
                    splittedSubtitle.Paragraphs.Add(new Paragraph(p));
            }
            listViewFixes.ItemChecked += listViewFixes_ItemChecked;
            splittedSubtitle.Renumber(1);
            return splittedSubtitle;
        }

        private void NumericUpDownMaxCharactersValueChanged(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            GeneratePreview(true);
            Cursor = Cursors.Default;
        }

        private void listViewFixes_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listViewFixes.SelectedIndices.Count > 0)
            {
                int index = listViewFixes.SelectedIndices[0];
                ListViewItem item = listViewFixes.Items[index];
                index = int.Parse(item.SubItems[1].Text) - 1;
                SubtitleListview1.SelectIndexAndEnsureVisible(index);
            }
        }

        private void SplitLongLines_Shown(object sender, EventArgs e)
        {
            GeneratePreview(true);
            listViewFixes.Focus();
            if (listViewFixes.Items.Count > 0)
                listViewFixes.Items[0].Selected = true;
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.OK;
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
        }

        private void labelMaxSingleLineLengthIs_Click(object sender, EventArgs e)
        {
            int index = int.Parse(labelMaxSingleLineLengthIs.Tag.ToString());
            SubtitleListview1.SelectIndexAndEnsureVisible(index);
        }

        private void labelMaxLineLengthIs_Click(object sender, EventArgs e)
        {
            int index = int.Parse(labelMaxLineLengthIs.Tag.ToString());
            SubtitleListview1.SelectIndexAndEnsureVisible(index);
        }

        private void ContinuationBeginEndChanged(object sender, EventArgs e)
        {
            Cursor = Cursors.WaitCursor;
            int index = -1;
            if (listViewFixes.SelectedItems.Count > 0)
                index = listViewFixes.SelectedItems[0].Index;
            GeneratePreview(true);
            if (index >= 0)
                listViewFixes.Items[index].Selected = true;
            Cursor = Cursors.Default;
        }

    }
}
