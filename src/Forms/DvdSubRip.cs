﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Text;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Logic;
using Nikse.SubtitleEdit.Logic.VobSub;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class DvdSubRip : Form
    {
        private volatile bool _abort;
        public List<VobSubMergedPack> MergedVobSubPacks;
        public List<Color> Palette;
        public List<string> Languages;
        private LanguageStructure.DvdSubRip _language;
        private long _lastPresentationTimeStamp = 0;
        private long _lastVobPresentationTimeStamp = 0;
        private long _lastNavEndPts = 0;
        private long _accumulatedPresentationTimeStamp;

        public string SelectedLanguage
        {
            get
            {
                if (comboBoxLanguages.SelectedIndex >= 0)
                    return string.Format("{0} (0x{1:x})", comboBoxLanguages.Items[comboBoxLanguages.SelectedIndex], comboBoxLanguages.SelectedIndex + 32);
                return string.Empty;
            }
        }

        public DvdSubRip()
        {
            InitializeComponent();
            labelStatus.Text = string.Empty;
            buttonStartRipping.Enabled = false;

            _language = Configuration.Settings.Language.DvdSubRip;
            Text = _language.Title;
            groupBoxDvd.Text = _language.DvdGroupTitle;
            labelIfoFile.Text = _language.IfoFile;
            labelVobFiles.Text = _language.VobFiles;
            groupBoxLanguages.Text = _language.Languages;
            groupBoxPalNtsc.Text = _language.PalNtsc;
            radioButtonPal.Text = _language.Pal;
            radioButtonNtsc.Text = _language.Ntsc;
            buttonStartRipping.Text = _language.StartRipping;
            buttonAddVobFile.Text = _language.Add;
            ButtonRemoveVob.Text = _language.Remove;
            buttonClear.Text = _language.Clear;
            ButtonMoveVobDown.Text = _language.MoveDown;
            ButtonMoveVobUp.Text = _language.MoveUp;

            if (System.Threading.Thread.CurrentThread.CurrentCulture.TwoLetterISOLanguageName == "en")
                radioButtonNtsc.Checked = true;
            else
                radioButtonPal.Checked = true;

            FixLargeFonts();
        }

        private void FixLargeFonts()
        {
            Graphics graphics = this.CreateGraphics();
            SizeF textSize = graphics.MeasureString(buttonAddVobFile.Text, this.Font);
            if (textSize.Height > buttonAddVobFile.Height - 4)
            {
                int newButtonHeight = (int)(textSize.Height + 7 + 0.5);
                Utilities.SetButtonHeight(this, newButtonHeight, 1);
            }
        }

        private void ButtonOpenIfoClick(object sender, EventArgs e)
        {
            openFileDialog1.Multiselect = false;
            openFileDialog1.Filter = _language.IfoFiles + "|*.IFO";
            openFileDialog1.FileName = string.Empty;
            if (openFileDialog1.ShowDialog() == DialogResult.OK && File.Exists(openFileDialog1.FileName))
            {
                OpenIfoFile(openFileDialog1.FileName);
            }
        }

        private void OpenIfoFile(string fileName)
        {
            Clear();
            textBoxIfoFileName.Text = fileName;

            // List vob files
            string path = Path.GetDirectoryName(fileName);
            string onlyFileName = Path.GetFileNameWithoutExtension(fileName);
            string searchPattern = onlyFileName.Substring(0, onlyFileName.Length - 1) + "?.VOB";
            listBoxVobFiles.Items.Clear();
            for (int i = 1; i < 30; i++)
            {
                string vobFileName = searchPattern.Replace("?", i.ToString());
                if (File.Exists(Path.Combine(path, vobFileName)))
                    listBoxVobFiles.Items.Add(Path.Combine(path, vobFileName));
            }

            if (listBoxVobFiles.Items.Count == 0)
            {
                searchPattern = onlyFileName.Substring(0, onlyFileName.Length - 1) + "PGC_01_?.VOB";
                for (int i = 1; i < 30; i++)
                {
                    string vobFileName = searchPattern.Replace("?", i.ToString());
                    if (File.Exists(Path.Combine(path, vobFileName)))
                        listBoxVobFiles.Items.Add(Path.Combine(path, vobFileName));
                }
            }

            var ifoParser = new IfoParser(fileName);
            if (!string.IsNullOrEmpty(ifoParser.ErrorMessage))
            {
                Clear();
                MessageBox.Show(ifoParser.ErrorMessage);
                return;
            }

            // List info
            labelIfoFile.Text = _language.IfoFile + ": " + ifoParser.VideoTitleSetVobs.VideoStream.Resolution;
            bool isPal = string.Compare(ifoParser.VideoTitleSetVobs.VideoStream.Standard, "PAL", true) == 0;
            if (isPal)
                radioButtonPal.Checked = true;
            else
                radioButtonNtsc.Checked = true;

            // List languages
            comboBoxLanguages.Items.Clear();
            foreach (string s in ifoParser.VideoTitleSetVobs.Subtitles)
                comboBoxLanguages.Items.Add(s);
            if (comboBoxLanguages.Items.Count > 0)
                comboBoxLanguages.SelectedIndex = 0;

            // Save palette (Color LookUp Table)
            if (ifoParser.VideoTitleSetProgramChainTable.ProgramChains.Count > 0)
                Palette = ifoParser.VideoTitleSetProgramChainTable.ProgramChains[0].ColorLookupTable;

            buttonStartRipping.Enabled = listBoxVobFiles.Items.Count > 0;
        }

        private void ButtonStartRippingClick(object sender, EventArgs e)
        {
            if (buttonStartRipping.Text == _language.Abort)
            {
                _abort = true;
                buttonStartRipping.Text = _language.StartRipping;
                return;
            }
            _abort = false;
            buttonStartRipping.Text = _language.Abort;
            _lastPresentationTimeStamp = 0;
            _lastVobPresentationTimeStamp = 0;
            _lastNavEndPts = 0;
            _accumulatedPresentationTimeStamp = 0;

            progressBarRip.Visible = true;
            var ms = new MemoryStream();
            int i = 0;
            foreach (string vobFileName in listBoxVobFiles.Items)
            {
                i++;
                labelStatus.Text = string.Format(_language.RippingVobFileXofYZ, Path.GetFileName(vobFileName), i, listBoxVobFiles.Items.Count);
                Refresh();
                Application.DoEvents();

                if (!_abort)
                    RipSubtitles(vobFileName, ms, i - 1); // Rip/demux subtitle vob packs
            }
            progressBarRip.Visible = false;
            buttonStartRipping.Enabled = false;
            if (_abort)
            {
                labelStatus.Text = _language.AbortedByUser;
                buttonStartRipping.Text = _language.StartRipping;
                buttonStartRipping.Enabled = true;
                return;
            }

            labelStatus.Text = string.Format(_language.ReadingSubtitleData);
            Refresh();
            Application.DoEvents();
            var vobSub = new VobSubParser(radioButtonPal.Checked);
            vobSub.Open(ms);
            ms.Close();
            labelStatus.Text = string.Empty;

            MergedVobSubPacks = vobSub.MergeVobSubPacks(); // Merge splitted-packs to whole-packs
            if (MergedVobSubPacks.Count == 0)
            {
                MessageBox.Show(Configuration.Settings.Language.Main.NoSubtitlesFound);
                buttonStartRipping.Text = _language.StartRipping;
                buttonStartRipping.Enabled = true;
                return;
            }
            Languages = new List<string>();
            for (int k = 0; k < comboBoxLanguages.Items.Count; k++)
                Languages.Add(string.Format("{0} (0x{1:x})", comboBoxLanguages.Items[k], k + 32));

            buttonStartRipping.Text = _language.StartRipping;
            buttonStartRipping.Enabled = true;
            DialogResult = DialogResult.OK;
        }

        private void RipSubtitles(string vobFileName, MemoryStream stream, int vobNumber)
        {
            long firstNavStartPTS = 0;

            FileStream fs = null;
            bool tryAgain = true;
            while (tryAgain)
            {
                try
                {
                    fs = new FileStream(vobFileName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    tryAgain = false;
                }
                catch (IOException exception)
                {
                    var result = MessageBox.Show(string.Format("An error occured while opening file: {0}", exception.Message), "", MessageBoxButtons.RetryCancel);
                    if (result == DialogResult.Cancel)
                        return;
                    if (result == DialogResult.Retry)
                        tryAgain = true;
                }
            }

            byte[] buffer = new byte[0x800];
            long position = 0;
            progressBarRip.Maximum = 100;
            progressBarRip.Value = 0;
            int lba = 0;
            long length = fs.Length;
            while (position < length && !_abort)
            {
                int bytesRead = 0;

                // Reading and test for IO errors... and allow abort/retry/ignore
                tryAgain = true;
                while (tryAgain && position < length)
                {
                    tryAgain = false;
                    try
                    {
                        fs.Seek(position, SeekOrigin.Begin);
                        bytesRead = fs.Read(buffer, 0, 0x0800);
                    }
                    catch (IOException exception)
                    {
                        var result = MessageBox.Show(string.Format("An error occured while reading file: {0}", exception.Message), "", MessageBoxButtons.AbortRetryIgnore);
                        if (result == DialogResult.Abort)
                            return;
                        if (result == DialogResult.Retry)
                            tryAgain = true;
                        if (result == DialogResult.Ignore)
                        {
                            position += 0x800;
                            tryAgain = true;
                        }
                    }
                }

                if (VobSubParser.IsMpeg2PackHeader(buffer))
                {
                    VobSubPack vsp = new VobSubPack(buffer, null);
                    if (IsSubtitlePack(buffer))
                    {
                        if (vsp.PacketizedElementaryStream.PresentationTimeStamp.HasValue && _accumulatedPresentationTimeStamp != 0)
                            UpdatePresentationTimeStamp(buffer, _accumulatedPresentationTimeStamp, vsp);

                        stream.Write(buffer, 0, 0x800);
                        if (bytesRead < 0x800)
                            stream.Write(Encoding.ASCII.GetBytes(new string(' ', 0x800 - bytesRead)), 0, 0x800 - bytesRead);
                    }
                    else if (IsPrivateStream2(buffer, 0x26))
                    {
                        if (Helper.GetEndian(buffer, 0x0026, 4) == 0x1bf && Helper.GetEndian(buffer, 0x400, 4) == 0x1bf)
                        {
                            uint vobu_s_ptm = Helper.GetEndian(buffer, 0x0039, 4);
                            uint vobu_e_ptm = Helper.GetEndian(buffer, 0x003d, 4);

                            _lastPresentationTimeStamp = vobu_e_ptm;

                            if (firstNavStartPTS == 0)
                            {
                                firstNavStartPTS = vobu_s_ptm;
                                if (vobNumber == 0)
                                    _accumulatedPresentationTimeStamp = -vobu_s_ptm;
                            }
                            if (vobu_s_ptm + firstNavStartPTS + _accumulatedPresentationTimeStamp < _lastVobPresentationTimeStamp)
                            {
                                _accumulatedPresentationTimeStamp += _lastNavEndPts - vobu_s_ptm;
                            }
                            else if (_lastNavEndPts > vobu_e_ptm)
                            {
                                _accumulatedPresentationTimeStamp += _lastNavEndPts - vobu_s_ptm;
                            }
                            _lastNavEndPts = vobu_e_ptm;
                        }
                    }

                }
                position += 0x800;

                progressBarRip.Value = (int)((position * 100) / length);
                Application.DoEvents();
                lba++;
            }
            fs.Close();
            _lastVobPresentationTimeStamp = _lastPresentationTimeStamp;
        }

        /// <summary>
        /// Write the 5 PTS bytes to buffer
        /// </summary>
        private static void UpdatePresentationTimeStamp(byte[] buffer, long addPresentationTimeStamp, VobSubPack vsp)
        {
            const int presentationTimeStampIndex = 23;
            long newPts = addPresentationTimeStamp + ((long)vsp.PacketizedElementaryStream.PresentationTimeStamp.Value);

            var buffer5b = BitConverter.GetBytes((UInt64)newPts);
            if (BitConverter.IsLittleEndian)
            {
                buffer[presentationTimeStampIndex + 4] = (byte)(buffer5b[0] << 1 | Helper.B00000001); // last 7 bits + '1'
                buffer[presentationTimeStampIndex + 3] = (byte)((buffer5b[0] >> 7) + (buffer5b[1] << 1)); // the next 8 bits (1 from last byte, 7 from next)
                buffer[presentationTimeStampIndex + 2] = (byte)((buffer5b[1] >> 6 | Helper.B00000001) + (buffer5b[2] << 2)); // the next 7 bits (1 from 2nd last byte, 6 from 3rd last byte)
                buffer[presentationTimeStampIndex + 1] = (byte)((buffer5b[2] >> 6) + (buffer5b[3] << 2)); // the next 8 bits (2 from 3rd last byte, 6 from 2rd last byte)
                buffer[presentationTimeStampIndex] = (byte)((buffer5b[3] >> 6 | Helper.B00000001) + (buffer5b[4] << 2));
            }
            else
            {
                buffer[presentationTimeStampIndex + 4] = (byte)(buffer5b[7] << 1 | Helper.B00000001); // last 7 bits + '1'
                buffer[presentationTimeStampIndex + 3] = (byte)((buffer5b[7] >> 7) + (buffer5b[6] << 1)); // the next 8 bits (1 from last byte, 7 from next)
                buffer[presentationTimeStampIndex + 2] = (byte)((buffer5b[6] >> 6 | Helper.B00000001) + (buffer5b[5] << 2)); // the next 7 bits (1 from 2nd last byte, 6 from 3rd last byte)
                buffer[presentationTimeStampIndex + 1] = (byte)((buffer5b[5] >> 6) + (buffer5b[4] << 2)); // the next 8 bits (2 from 3rd last byte, 6 from 2rd last byte)
                buffer[presentationTimeStampIndex] = (byte)((buffer5b[4] >> 6 | Helper.B00000001) + (buffer5b[3] << 2));
            }
            if (vsp.PacketizedElementaryStream.PresentationTimeStampDecodeTimeStampFlags == Helper.B00000010)
                buffer[presentationTimeStampIndex] += Helper.B00100000;
            else
                buffer[presentationTimeStampIndex] += Helper.B00110000;
        }

        internal static bool IsPrivateStream2(byte[] buffer, int index)
        {
            return buffer.Length >= index + 3 &&
                   buffer[index + 0] == 0 &&
                   buffer[index + 1] == 0 &&
                   buffer[index + 2] == 1 &&
                   buffer[index + 3] == 0xbf;
        }

        private static bool IsSubtitlePack(byte[] buffer)
        {
            const int mpeg2HeaderLength = 14;
            if (buffer[0] == 0 &&
                buffer[1] == 0 &&
                buffer[2] == 1 &&
                buffer[3] == 0xba) // 186 - MPEG-2 Pack Header
            {
                if (buffer[mpeg2HeaderLength + 0] == 0 &&
                    buffer[mpeg2HeaderLength + 1] == 0 &&
                    buffer[mpeg2HeaderLength + 2] == 1 &&
                    buffer[mpeg2HeaderLength + 3] == 0xbd) // 189 - Private stream 1 (non MPEG audio, subpictures)
                {
                    int pesHeaderDataLength = buffer[mpeg2HeaderLength + 8];
                    int streamId = buffer[mpeg2HeaderLength + 8 + 1 + pesHeaderDataLength];
                    if (streamId >= 0x20 && streamId <= 0x3f) // Subtitle IDs allowed (or x3f to x40?)
                        return true;
                }
            }
            return false;
        }

        private void ButtonAddVobFileClick(object sender, EventArgs e)
        {
            openFileDialog1.Filter = _language.VobFiles + "|*.VOB";
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Multiselect = true;
            if (openFileDialog1.ShowDialog() == DialogResult.OK && File.Exists(openFileDialog1.FileName))
            {
                foreach (var fileName in openFileDialog1.FileNames)
                {
                    listBoxVobFiles.Items.Add(fileName);
                }
            }
            buttonStartRipping.Enabled = listBoxVobFiles.Items.Count > 0;
        }

        private void ButtonMoveVobUp_Click(object sender, EventArgs e)
        {
            if (listBoxVobFiles.SelectedIndex > -1 && listBoxVobFiles.SelectedIndex > 0)
            {
                int index = listBoxVobFiles.SelectedIndex;
                string old = listBoxVobFiles.Items[index].ToString();
                listBoxVobFiles.Items.RemoveAt(index);
                listBoxVobFiles.Items.Insert(index - 1, old);
                listBoxVobFiles.SelectedIndex = index - 1;
            }
        }

        private void ButtonMoveVobDown_Click(object sender, EventArgs e)
        {
            if (listBoxVobFiles.SelectedIndex > -1 && listBoxVobFiles.SelectedIndex < listBoxVobFiles.Items.Count - 1)
            {
                int index = listBoxVobFiles.SelectedIndex;
                string old = listBoxVobFiles.Items[index].ToString();
                listBoxVobFiles.Items.RemoveAt(index);
                listBoxVobFiles.Items.Insert(index + 1, old);
                listBoxVobFiles.SelectedIndex = index + 1;
            }
        }

        private void ButtonRemoveVob_Click(object sender, EventArgs e)
        {
            if (listBoxVobFiles.SelectedIndex > -1)
            {
                int index = listBoxVobFiles.SelectedIndex;
                listBoxVobFiles.Items.RemoveAt(index);
                if (index < listBoxVobFiles.Items.Count)
                    listBoxVobFiles.SelectedIndex = index;
                else if (index > 0)
                    listBoxVobFiles.SelectedIndex = index - 1;

                buttonStartRipping.Enabled = listBoxVobFiles.Items.Count > 0;

                if (listBoxVobFiles.Items.Count == 0)
                    Clear();
            }
        }

        private void TextBoxIfoFileNameDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void TextBoxIfoFileNameDragDrop(object sender, DragEventArgs e)
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files.Length >= 1)
            {
                string fileName = files[0];

                var fi = new FileInfo(fileName);
                string ext = Path.GetExtension(fileName).ToLower();
                if (fi.Length < 1024 * 1024 * 2) // max 2 mb
                {
                    if (ext == ".ifo")
                        OpenIfoFile(fileName);
                }
            }
        }

        private void ListBoxVobFilesDragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;
        }

        private void ListBoxVobFilesDragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop, false))
                e.Effect = DragDropEffects.All;

            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            foreach (string fileName in files)
            {
                string ext = Path.GetExtension(fileName).ToLower();
                if (ext == ".vob")
                    listBoxVobFiles.Items.Add(fileName);
            }
            buttonStartRipping.Enabled = listBoxVobFiles.Items.Count > 0;
        }

        private void ButtonClearClick(object sender, EventArgs e)
        {
            Clear();
        }

        private void Clear()
        {
            textBoxIfoFileName.Text = string.Empty;
            listBoxVobFiles.Items.Clear();
            buttonStartRipping.Enabled = false;
            comboBoxLanguages.Items.Clear();
            labelIfoFile.Text = _language.IfoFile;
        }

        private void DvdSubRip_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                if (buttonStartRipping.Text == _language.Abort)
                {
                    ButtonStartRippingClick(sender, e);
                }
                else
                {
                    e.SuppressKeyPress = true;
                    DialogResult = DialogResult.Cancel;
                }
            }
        }

    }
}
