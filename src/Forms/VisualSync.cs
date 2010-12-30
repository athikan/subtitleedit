﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Forms;
using Nikse.SubtitleEdit.Controls;
using Nikse.SubtitleEdit.Logic;
using System.Drawing;

namespace Nikse.SubtitleEdit.Forms
{
    public sealed partial class VisualSync : Form
    {
        List<Paragraph> _paragraphs;
        VideoInfo _videoInfo;
        string _totalPositionString = string.Empty;
        string _subtitleFileName;
        Subtitle _originalSubtitle;
        double _oldFramerate;
        bool _frameRateChanged;
        bool _isStartSceneActive;
        double _startGoBackPosition;
        double _startStopPosition = -1.0;
        double _endGoBackPosition;
        double _endStopPosition = -1.0;
        readonly LanguageStructure.VisualSync _language;
        readonly LanguageStructure.General _languageGeneral;

        public string VideoFileName { get; set; }

        public bool OKPressed { get; set; }

        public bool FrameRateChanged
        {
            get { return _frameRateChanged; }
        }

        public double FrameRate
        {
            get 
            {
                if (_videoInfo == null)
                    return 0;
                return _videoInfo.FramesPerSecond; 
            }
        }

        public List<Paragraph> Paragraphs
        {
            get { return _paragraphs; }
        }

        public VisualSync()
        {
            InitializeComponent();

            labelStartSubtitle.Text = string.Empty;
            labelEndSubtitle.Text = string.Empty;
            openFileDialog1.InitialDirectory = string.Empty;

            MediaPlayerStart.InitializeVolume(Configuration.Settings.General.VideoPlayerDefaultVolume);
            MediaPlayerEnd.InitializeVolume(Configuration.Settings.General.VideoPlayerDefaultVolume);

            _language = Configuration.Settings.Language.VisualSync;
            _languageGeneral = Configuration.Settings.Language.General;
            Text = _language.Title;
            buttonOpenMovie.Text = _languageGeneral.OpenVideoFile;
            groupBoxMovieInfo.Text = _languageGeneral.VideoInformation;
            labelVideoInfo.Text = _languageGeneral.NoVideoLoaded;
            groupBoxStartScene.Text = _language.StartScene;
            groupBoxEndScene.Text = _language.EndScene;
            buttonStartThreeSecondsBack.Text = _language.ThreeSecondsBack;
            buttonThreeSecondsBack.Text = _language.ThreeSecondsBack;
            buttonStartHalfASecondBack.Text = _language.HalfASecondBack;
            buttonEndHalfASecondBack.Text = _language.HalfASecondBack;
            buttonStartVerify.Text = string.Format(_language.PlayXSecondsAndBack, Configuration.Settings.Tools.VerifyPlaySeconds);
            buttonEndVerify.Text = buttonStartVerify.Text;
            buttonGotoStartSubtitlePosition.Text = _language.GoToSubPosition;
            buttonGotoEndSubtitlePosition.Text = _language.GoToSubPosition;
            buttonFindTextStart.Text = _language.FindText;
            buttonFindTextEnd.Text = _language.FindText;
            buttonSync.Text = _language.Synchronize;
            buttonOK.Text = Configuration.Settings.Language.General.OK;
            buttonCancel.Text = Configuration.Settings.Language.General.Cancel;
            labelTip.Text = _language.Tip;
            FixLargeFonts();
        }

        private void FixLargeFonts()
        {
            Graphics graphics = this.CreateGraphics();
            SizeF textSize = graphics.MeasureString(buttonCancel.Text, this.Font);
            if (textSize.Height > buttonCancel.Height - 4)
            {
                int newButtonHeight = (int)(textSize.Height + 7 + 0.5);
                Utilities.SetButtonHeight(this, newButtonHeight, 1);
            }
        }

        private void GotoSubtitlePosition(VideoPlayerContainer mediaPlayer)
        {
            int index;
            if (mediaPlayer == MediaPlayerStart)
                index = comboBoxStartTexts.SelectedIndex;
            else
                index = comboBoxEndTexts.SelectedIndex;

            mediaPlayer.Pause();
            if (index != -1)
            {
                double indexPositionInSeconds = _paragraphs[index].StartTime.TotalMilliseconds / 1000.0;

                if (indexPositionInSeconds > mediaPlayer.Duration)
                    indexPositionInSeconds = mediaPlayer.Duration - (2 * 60);

                if (indexPositionInSeconds < 0)
                    indexPositionInSeconds = 0;
                
                mediaPlayer.CurrentPosition = indexPositionInSeconds;
                mediaPlayer.RefreshProgressBar();
            }
        }

        private void OpenVideo(string fileName)
        {
            if (File.Exists(fileName))
            {
                VideoFileName = fileName;

                FileInfo fi = new FileInfo(fileName);
                if (fi.Length < 1000)
                    return;

                if (MediaPlayerStart.VideoPlayer != null)
                {
                    MediaPlayerStart.Pause();
                    MediaPlayerStart.VideoPlayer.DisposeVideoPlayer();
                }
                if (MediaPlayerEnd.VideoPlayer != null)
                {
                    MediaPlayerEnd.Pause();
                    MediaPlayerEnd.VideoPlayer.DisposeVideoPlayer();
                }

                VideoInfo videoInfo = ShowVideoInfo(fileName);

                // be sure to match frames with movie
                if (_originalSubtitle.WasLoadedWithFrameNumbers) // frame based subtitles like micro dvd
                {
                    if (Math.Abs(_videoInfo.FramesPerSecond - _oldFramerate) > 0.02)
                    {
                        _originalSubtitle.CalculateTimeCodesFromFrameNumbers(_videoInfo.FramesPerSecond);
                        LoadAndShowOriginalSubtitle();
                        _frameRateChanged = true;
                    }
                }

                Utilities.InitializeVideoPlayerAndContainer(fileName, videoInfo, MediaPlayerStart, VideoStartLoaded, VideoStartEnded);
            }
        }

        void VideoStartEnded(object sender, EventArgs e)
        {
            MediaPlayerStart.Pause();
        }

        void VideoStartLoaded(object sender, EventArgs e)
        {
            MediaPlayerStart.Pause();
            GotoSubtitlePosition(MediaPlayerStart);

            _startGoBackPosition = MediaPlayerStart.CurrentPosition;
            _startStopPosition = _startGoBackPosition + 0.1;
            MediaPlayerStart.Play();
           
            if (MediaPlayerStart.VideoPlayer.GetType() == typeof(Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic))            
            {
                MediaPlayerEnd.VideoPlayer = (MediaPlayerStart.VideoPlayer as Nikse.SubtitleEdit.Logic.VideoPlayers.LibVlc11xDynamic).MakeSecondMediaPlayer(MediaPlayerEnd.PanelPlayer, VideoFileName, VideoEndLoaded, VideoEndEnded);
            }
            else
            {
                Utilities.InitializeVideoPlayerAndContainer(MediaPlayerStart.VideoPlayer.VideoFileName, null, MediaPlayerEnd, VideoEndLoaded, VideoEndEnded);
            }
            timer1.Start();
        }

        void VideoEndEnded(object sender, EventArgs e)
        {
            MediaPlayerEnd.Pause();
        }

        void VideoEndLoaded(object sender, EventArgs e)
        {
            MediaPlayerEnd.Pause();
            GotoSubtitlePosition(MediaPlayerEnd);

            _endGoBackPosition = MediaPlayerEnd.CurrentPosition;
            _endStopPosition = _endGoBackPosition + 0.1;
            MediaPlayerEnd.Play();
        }

        private VideoInfo ShowVideoInfo(string fileName)
        {
            _videoInfo = Utilities.GetVideoInfo(fileName, delegate { Application.DoEvents(); });
            var info = new FileInfo(fileName);
            long fileSizeInBytes = info.Length;

            labelVideoInfo.Text = string.Format(_languageGeneral.FileNameXAndSize, fileName, Utilities.FormatBytesToDisplayFileSize(fileSizeInBytes)) + Environment.NewLine +
                                  string.Format(_languageGeneral.ResolutionX, +_videoInfo.Width + "x" + _videoInfo.Height) + "    ";
            if (_videoInfo.FramesPerSecond > 5 && _videoInfo.FramesPerSecond < 200)
                labelVideoInfo.Text += string.Format(_languageGeneral.FrameRateX + "        ", _videoInfo.FramesPerSecond);
            if (_videoInfo.TotalFrames > 10)
                labelVideoInfo.Text += string.Format(_languageGeneral.TotalFramesX + "         ", (int)_videoInfo.TotalFrames);
            if (!string.IsNullOrEmpty(_videoInfo.VideoCodec))
                labelVideoInfo.Text += string.Format(_languageGeneral.VideoEncodingX, _videoInfo.VideoCodec) + "        ";

            TimeSpan span = TimeSpan.FromMilliseconds(_videoInfo.TotalMilliseconds);
            _totalPositionString = " / " + string.Format("{0:00}:{1:00}:{2:00},{3:000}", span.Hours, span.Minutes, span.Seconds, span.Milliseconds);

            return _videoInfo;
        }

        private void Timer1Tick(object sender, EventArgs e)
        {
            if (MediaPlayerStart != null)
            {
                if (!MediaPlayerStart.IsPaused)
                {
                    MediaPlayerStart.RefreshProgressBar();
                    if (_startStopPosition >= 0 && MediaPlayerStart.CurrentPosition > _startStopPosition)
                    {
                        MediaPlayerStart.Pause();
                        MediaPlayerStart.CurrentPosition = _startGoBackPosition;
                        _startStopPosition = -1;
                    }
                    Utilities.ShowSubtitle(_paragraphs, labelStartSubtitle, MediaPlayerStart.VideoPlayer);
                }
                if (!MediaPlayerEnd.IsPaused)
                {
                    MediaPlayerEnd.RefreshProgressBar();
                    if (_endStopPosition >= 0 && MediaPlayerEnd.CurrentPosition > _endStopPosition)
                    {
                        MediaPlayerEnd.Pause();
                        MediaPlayerEnd.CurrentPosition = _endGoBackPosition;
                        _endStopPosition = -1;
                    }
                    Utilities.ShowSubtitle(_paragraphs, labelEndSubtitle, MediaPlayerEnd.VideoPlayer);
                }                
            }
        }
       
        private void FormVisualSync_FormClosing(object sender, FormClosingEventArgs e)
        {
            timer1.Stop();

            if (MediaPlayerStart != null)
                MediaPlayerStart.Pause();
            if (MediaPlayerEnd != null)
                MediaPlayerEnd.Pause();

            bool change = false;
            for (int i=0; i<_paragraphs.Count; i++)
            { 
                if (_paragraphs[i].ToString() != _originalSubtitle.Paragraphs[i].ToString())
                {
                    change = true;
                    break;
                }
            }

            if (!change)
            {
                DialogResult = DialogResult.Cancel;
                return;
            }

            DialogResult dr;
            if (DialogResult == DialogResult.OK)
                dr = DialogResult.Yes;
            else
                dr = MessageBox.Show(_language.KeepChangesMessage + Environment.NewLine +
                                     _language.KeepChangesTitle, _language.KeepChangesTitle, MessageBoxButtons.YesNoCancel);

            if (dr == DialogResult.Cancel)
            {
                e.Cancel = true;
                timer1.Start();
            }
            else if (dr == DialogResult.Yes)
            {
                DialogResult = DialogResult.OK;
            }
            else
            {
                DialogResult = DialogResult.Cancel;
            }
        }

        internal void Initialize(Bitmap bitmap, Subtitle subtitle, string fileName, string title, double framerate)
        {
            if (bitmap != null)
            {
                IntPtr Hicon = bitmap.GetHicon();
                this.Icon = System.Drawing.Icon.FromHandle(Hicon);
            }

            _originalSubtitle = subtitle;
            _oldFramerate = framerate;
            _subtitleFileName = fileName;
            Text = title;
        }

        private void LoadAndShowOriginalSubtitle()
        {
            _paragraphs = new List<Paragraph>();
            foreach (Paragraph p in _originalSubtitle.Paragraphs)
                _paragraphs.Add(new Paragraph(p));

            FillStartAndEndTexts();

            if (comboBoxStartTexts.Items.Count > Configuration.Settings.Tools.StartSceneIndex)
                comboBoxStartTexts.SelectedIndex = Configuration.Settings.Tools.StartSceneIndex;

            if (comboBoxEndTexts.Items.Count > Configuration.Settings.Tools.EndSceneIndex)
                comboBoxEndTexts.SelectedIndex = comboBoxEndTexts.Items.Count - (Configuration.Settings.Tools.EndSceneIndex + 1);
        }

        private void FillStartAndEndTexts()
        {
            comboBoxStartTexts.BeginUpdate();
            comboBoxEndTexts.BeginUpdate();
            comboBoxStartTexts.Items.Clear();
            comboBoxEndTexts.Items.Clear();
            foreach (Paragraph p in _paragraphs)
            {
                string s = p.StartTime + " - " + p.Text.Replace(Environment.NewLine, Configuration.Settings.General.ListViewLineSeparatorString);
                comboBoxStartTexts.Items.Add(s);
                comboBoxEndTexts.Items.Add(s);
            }
            comboBoxStartTexts.EndUpdate();
            comboBoxEndTexts.EndUpdate();
        }

        private void TryToFindAndOpenMovieFile(string fileNameNoExtension)
        {
            string movieFileName = null;

            foreach (string extension in Utilities.GetMovieFileExtensions())
            {
                movieFileName = fileNameNoExtension + extension;
                if (File.Exists(movieFileName))
                    break;
            }

            if (movieFileName != null && File.Exists(movieFileName))
            {
                OpenVideo(movieFileName);
            }
            else if (fileNameNoExtension.Contains("."))
            {
                fileNameNoExtension = fileNameNoExtension.Substring(0, fileNameNoExtension.LastIndexOf("."));
                TryToFindAndOpenMovieFile(fileNameNoExtension);
            }
        }

        private void ButtonGotoStartSubtitlePositionClick(object sender, EventArgs e)
        {
            GotoSubtitlePosition(MediaPlayerStart);
        }

        private void ButtonGotoEndSubtitlePositionClick(object sender, EventArgs e)
        {
            GotoSubtitlePosition(MediaPlayerEnd);
        }

        private void ButtonSyncClick(object sender, EventArgs e)
        {
            double startPos = MediaPlayerStart.CurrentPosition;
            double endPos = MediaPlayerEnd.CurrentPosition;
            if (endPos > startPos)
            {
                double subStart = _paragraphs[comboBoxStartTexts.SelectedIndex].StartTime.TotalMilliseconds / 1000.0;
                double subEnd = _paragraphs[comboBoxEndTexts.SelectedIndex].StartTime.TotalMilliseconds / 1000.0;

                double subDiff = subEnd - subStart;
                double realDiff = endPos - startPos;

                // speed factor
                double factor = realDiff / subDiff;

                // adjust to starting position
                double adjust = startPos - subStart * factor;

                foreach (Paragraph p in _paragraphs)
                {
                    p.Adjust(factor, adjust);
                }

                // fix overlapping time codes
                var formFix = new FixCommonErrors();
                var tmpSubtitle = new Subtitle { WasLoadedWithFrameNumbers = _originalSubtitle.WasLoadedWithFrameNumbers };
                foreach (Paragraph p in _paragraphs)
                    tmpSubtitle.Paragraphs.Add(new Paragraph(p));
                formFix.Initialize(tmpSubtitle);
                formFix.FixOverlappingDisplayTimes();
                _paragraphs.Clear();
                foreach (Paragraph p in formFix.FixedSubtitle.Paragraphs)
                    _paragraphs.Add(new Paragraph(p));

                // update comboboxes
                int startSaveIdx = comboBoxStartTexts.SelectedIndex;
                int endSaveIdx = comboBoxEndTexts.SelectedIndex;
                FillStartAndEndTexts();
                comboBoxStartTexts.SelectedIndex = startSaveIdx;
                comboBoxEndTexts.SelectedIndex = endSaveIdx;

                MessageBox.Show(_language.SynchronizationDone, "", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            else
            {
                MessageBox.Show(_language.StartSceneMustComeBeforeEndScene, "", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }

        private void GoBackSeconds(double seconds, Label labelSubtitle, VideoPlayerContainer mediaPlayer)
        {
            if (mediaPlayer.CurrentPosition > seconds)
                mediaPlayer.CurrentPosition -= seconds;
            else
                mediaPlayer.CurrentPosition = 0;
            Utilities.ShowSubtitle(_paragraphs, labelSubtitle, mediaPlayer.VideoPlayer);
        }

        private void ButtonStartHalfASecondBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(0.5, labelStartSubtitle, MediaPlayerStart);
        }

        private void ButtonStartThreeSecondsBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(3.0, labelStartSubtitle, MediaPlayerStart);
        }

        private void ButtonEndHalfASecondBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(0.5, labelEndSubtitle, MediaPlayerEnd);
        }

        private void ButtonThreeSecondsBackClick(object sender, EventArgs e)
        {
            GoBackSeconds(3.0, labelEndSubtitle, MediaPlayerEnd);
        }

        private void ButtonOpenMovieClick(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(openFileDialog1.InitialDirectory) && !string.IsNullOrEmpty(_subtitleFileName))
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(_subtitleFileName);
            openFileDialog1.Title = _languageGeneral.OpenVideoFile;
            openFileDialog1.FileName = string.Empty;
            openFileDialog1.Filter = Utilities.GetVideoFileFilter();
            openFileDialog1.FileName = string.Empty;
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                openFileDialog1.InitialDirectory = Path.GetDirectoryName(openFileDialog1.FileName);
                OpenVideo(openFileDialog1.FileName);
            }
        }

        private void SizeWmp()
        {
            MediaPlayerStart.Left = labelStartSubtitle.Left;
            MediaPlayerStart.Width = labelStartSubtitle.Width;
            MediaPlayerStart.Height = labelStartSubtitle.Top - MediaPlayerStart.Top;
            MediaPlayerStart.RefreshProgressBar();

            MediaPlayerEnd.Left = labelEndSubtitle.Left;
            MediaPlayerEnd.Width = labelEndSubtitle.Width;
            MediaPlayerEnd.Height = labelEndSubtitle.Top - MediaPlayerEnd.Top;
            MediaPlayerEnd.RefreshProgressBar();
        }

        private void FormVisualSync_Resize(object sender, EventArgs e)
        {
            int halfWidth = Width / 2;
            groupBoxStartScene.Width = halfWidth - 18;
            labelStartSubtitle.Width = groupBoxStartScene.Width - 12;
            panelControlsStart.Width = labelStartSubtitle.Width;

            groupBoxEndScene.Left = halfWidth + 3;
            groupBoxEndScene.Width = halfWidth - 18;
            labelEndSubtitle.Width = groupBoxEndScene.Width - 12;
            SizeWmp();
            panelControlsEnd.Width = MediaPlayerEnd.Width;
            groupBoxStartScene.Height = Height - groupBoxEndScene.Top - 90;
            groupBoxEndScene.Height = Height - groupBoxEndScene.Top - 90;
            SizeWmp();
        }

        private void ButtonFindTextStartClick(object sender, EventArgs e)
        {
            var findSubtitle = new FindSubtitleLine();
            findSubtitle.Initialize(_paragraphs, " " + "(" + _language.StartScene.ToLower() + ")");
            findSubtitle.ShowDialog();
            if (findSubtitle.SelectedIndex >= 0)
            {
                comboBoxStartTexts.SelectedIndex = findSubtitle.SelectedIndex;
            }
            findSubtitle.Dispose();
        }

        private void ButtonFindTextEndClick(object sender, EventArgs e)
        {
            var findSubtitle = new FindSubtitleLine();
            findSubtitle.Initialize(_paragraphs, " " + "(" + _language.EndScene.ToLower() + ")");
            findSubtitle.ShowDialog();
            if (findSubtitle.SelectedIndex >= 0)
            {
                comboBoxEndTexts.SelectedIndex = findSubtitle.SelectedIndex;
            }
            findSubtitle.Dispose();
        }

        private void HighlightStartScene()
        {
            _isStartSceneActive = true;
            panelControlsStart.BorderStyle = BorderStyle.FixedSingle;
            panelControlsEnd.BorderStyle = BorderStyle.None;
        }

        private void HighlightEndScene()
        {
            _isStartSceneActive = false;
            panelControlsEnd.BorderStyle = BorderStyle.FixedSingle;
            panelControlsStart.BorderStyle = BorderStyle.None;
        }

        private void GroupBoxStartSceneEnter(object sender, EventArgs e)
        {
            HighlightStartScene();
        }

        private void GroupBoxEndSceneEnter(object sender, EventArgs e)
        {
            HighlightEndScene();
        }

        private void VisualSync_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                DialogResult = DialogResult.Cancel;
            }
            else if (e.KeyCode == Keys.F1)
            {
                Utilities.ShowHelp("#visual_sync");
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.O)
            {
                ButtonOpenMovieClick(null, null);
            }
            else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.F)
            {
                if (_isStartSceneActive)
                    ButtonFindTextStartClick(null, null);
                else
                    ButtonFindTextEndClick(null, null);
            }
            else if (MediaPlayerStart != null && MediaPlayerEnd != null)
            {
                if (e.Modifiers == Keys.Control && e.KeyCode == Keys.S)
                {
                    if (_isStartSceneActive)
                    {
                        _startStopPosition = -1;
                        if (!MediaPlayerStart.IsPaused)
                            MediaPlayerStart.Pause();
                    }
                    else
                    {
                        _endStopPosition = -1;
                        if (!MediaPlayerEnd.IsPaused)
                            MediaPlayerEnd.Pause();
                    }
                }
                else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.P)
                {
                    if (_isStartSceneActive)
                    {
                        _startStopPosition = -1;
                        MediaPlayerStart.TooglePlayPause();
                    }
                    else
                    {
                        _endStopPosition = -1;
                        MediaPlayerStart.TooglePlayPause();
                    }
                }
                else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.Left)
                {
                    if (_isStartSceneActive)
                        GoBackSeconds(0.5, labelStartSubtitle, MediaPlayerStart);
                    else
                        GoBackSeconds(0.5, labelEndSubtitle, MediaPlayerEnd);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.Alt && e.KeyCode == Keys.Right)
                {
                    if (_isStartSceneActive)
                        GoBackSeconds(-0.5, labelStartSubtitle, MediaPlayerStart);
                    else
                        GoBackSeconds(-0.5, labelEndSubtitle, MediaPlayerEnd);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Left)
                {
                    if (_isStartSceneActive)
                        GoBackSeconds(0.1, labelStartSubtitle, MediaPlayerStart);
                    else
                        GoBackSeconds(0.1, labelEndSubtitle, MediaPlayerEnd);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == Keys.Control && e.KeyCode == Keys.Right)
                {
                    if (_isStartSceneActive)
                        GoBackSeconds(-0.1, labelStartSubtitle, MediaPlayerStart);
                    else
                        GoBackSeconds(-0.1, labelEndSubtitle, MediaPlayerEnd);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.Right)
                {
                    if (_isStartSceneActive)
                        GoBackSeconds(1.0, labelStartSubtitle, MediaPlayerStart);
                    else
                        GoBackSeconds(1.0, labelEndSubtitle, MediaPlayerEnd);
                    e.SuppressKeyPress = true;
                }
                else if (e.Modifiers == (Keys.Control | Keys.Shift) && e.KeyCode == Keys.Left)
                {
                    if (_isStartSceneActive)
                        GoBackSeconds(-1.0, labelStartSubtitle, MediaPlayerStart);
                    else
                        GoBackSeconds(-1.0, labelEndSubtitle, MediaPlayerEnd);
                    e.SuppressKeyPress = true;
                }

            }
        }

        private void VisualSync_Shown(object sender, EventArgs e)
        {
            comboBoxStartTexts.Focus();
        }

        private void ButtonStartVerifyClick(object sender, EventArgs e)
        {
            if (MediaPlayerStart != null && MediaPlayerStart.VideoPlayer != null)
            {
                _startGoBackPosition = MediaPlayerStart.CurrentPosition;
                _startStopPosition = _startGoBackPosition + Configuration.Settings.Tools.VerifyPlaySeconds;
                MediaPlayerStart.Play();
            }
        }

        private void ButtonEndVerifyClick(object sender, EventArgs e)
        {
            if (MediaPlayerEnd != null && MediaPlayerEnd.VideoPlayer != null)
            {
                _endGoBackPosition = MediaPlayerEnd.CurrentPosition;
                _endStopPosition = _endGoBackPosition + Configuration.Settings.Tools.VerifyPlaySeconds;
                MediaPlayerEnd.Play();
            }
        }

        private void VisualSync_Load(object sender, EventArgs e)
        {
            LoadAndShowOriginalSubtitle();
            if (!string.IsNullOrEmpty(VideoFileName) && File.Exists(VideoFileName))
                OpenVideo(VideoFileName);
            else if (!string.IsNullOrEmpty(_subtitleFileName))
                TryToFindAndOpenMovieFile(Path.GetDirectoryName(_subtitleFileName) + Path.DirectorySeparatorChar + Path.GetFileNameWithoutExtension(_subtitleFileName));
            FormVisualSync_Resize(null, null);
        }

        private void MediaPlayerStart_OnButtonClicked(object sender, EventArgs e)
        {
            if (!_isStartSceneActive)
                HighlightStartScene();
        }

        private void MediaPlayerEnd_OnButtonClicked(object sender, EventArgs e)
        {
            if (_isStartSceneActive)
                HighlightEndScene();
        }

        private void VisualSync_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (MediaPlayerStart.VideoPlayer != null) // && MediaPlayerStart.VideoPlayer.GetType() == typeof(Nikse.SubtitleEdit.Logic.VideoPlayers.QuartsPlayer))
            {
                MediaPlayerStart.VideoPlayer.Pause();
                MediaPlayerStart.VideoPlayer.DisposeVideoPlayer();
            }
            if (MediaPlayerEnd.VideoPlayer != null) // && MediaPlayerEnd.VideoPlayer.GetType() == typeof(Nikse.SubtitleEdit.Logic.VideoPlayers.QuartsPlayer))
            {
                MediaPlayerEnd.VideoPlayer.Pause();
                MediaPlayerEnd.VideoPlayer.DisposeVideoPlayer();
            }
        }

        private void buttonOK_Click(object sender, EventArgs e)
        {
            OKPressed = true;
        }

    }
}
