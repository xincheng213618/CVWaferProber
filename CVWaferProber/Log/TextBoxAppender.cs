using System.Windows.Controls;
using log4net.Appender;
using log4net.Core;
using System.Windows.Threading;
using TextBox = System.Windows.Controls.TextBox;

namespace CVWaferProber.Log
{
    public class TextBoxAppender : AppenderSkeleton
    {
        private TextBox _textBox;
        private ScrollViewer _scrollViewer;
        private int _maxLines = 1000;

        public TextBox TargetTextBox
        {
            get { return _textBox; }
            set { _textBox = value; }
        }

        public ScrollViewer TargetScrollViewer
        {
            get { return _scrollViewer; }
            set { _scrollViewer = value; }
        }

        public int MaxLines
        {
            get { return _maxLines; }
            set { _maxLines = value; }
        }

        protected override void Append(LoggingEvent loggingEvent)
        {
            if (_textBox == null) return;

            string logMessage = RenderLoggingEvent(loggingEvent);

            // 在UI线程上执行更新
            _textBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                // 添加新日志
                _textBox.AppendText(logMessage + Environment.NewLine);

                // 限制日志行数
                if (_textBox.LineCount > _maxLines)
                {
                    int removeCount = _textBox.LineCount - _maxLines;
                    string[] lines = _textBox.Text.Split(new[] { Environment.NewLine }, StringSplitOptions.None);
                    _textBox.Text = string.Join(Environment.NewLine, lines.Skip(removeCount));
                }

                // 自动滚动到底部
                _textBox.ScrollToEnd();

                // 如果有ScrollViewer，也滚动到底部
                _scrollViewer?.ScrollToBottom();
            }), DispatcherPriority.Background);
        }
    }
}
