using log4net.Appender;
using log4net.Core;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Threading;

namespace CVWaferProber.Log
{
    public class TextBoxAppender : AppenderSkeleton
    {
        // 1. 替换原 TargetTextBox 为 TargetRichTextBox（RichTextBox类型）
        private RichTextBox _richTextBox;
        private ScrollViewer _scrollViewer;
        private int _maxLines = 1000;

        // 2. 定义 TargetRichTextBox 属性（用于绑定RichTextBox控件）
        public RichTextBox TargetRichTextBox
        {
            get { return _richTextBox; }
            set { _richTextBox = value; }
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

        // 3. 后续 Append 方法保持不变（已适配RichTextBox）
        protected override void Append(LoggingEvent loggingEvent)
        {
            if (_richTextBox == null) return;

            string logMessage = RenderLoggingEvent(loggingEvent);
            bool isErrorLevel = loggingEvent.Level == Level.Error;

            _richTextBox.Dispatcher.BeginInvoke(new Action(() =>
            {
                Paragraph logParagraph = new Paragraph
                {
                    // 1. 合理行高：14（匹配12号字体，紧凑不重叠），禁止设为0
                    LineHeight = 14,
                    // 2. 强制行堆叠策略，确保LineHeight生效
                    LineStackingStrategy = LineStackingStrategy.BlockLineHeight,
                    // 3. 核心：消除段落间默认边距，让日志整体紧凑
                    Margin = new Thickness(0) // 上下左右边距均为0，移除段落间空隙
                };
                Run logRun = new Run(logMessage) // 移除末尾的Environment.NewLine（避免额外换行，段落已自带换行）
                {
                    Foreground = isErrorLevel ? Brushes.Red : Brushes.Black,
                    FontSize = 12 // 明确字体大小，保证行高匹配
                };
                logParagraph.Inlines.Add(logRun);

                _richTextBox.Document.Blocks.Add(logParagraph);
                LimitLogLines();
                _richTextBox.ScrollToEnd();
                _scrollViewer?.ScrollToBottom();
            }), DispatcherPriority.Background);
        }

        // 4. 保留原有 LimitLogLines 方法（适配RichTextBox）
        private void LimitLogLines()
        {
            if (_richTextBox.Document.Blocks.Count <= _maxLines) return;

            int removeCount = _richTextBox.Document.Blocks.Count - _maxLines;
            for (int i = 0; i < removeCount; i++)
            {
                var firstParagraph = _richTextBox.Document.Blocks.FirstOrDefault();
                if (firstParagraph != null)
                {
                    _richTextBox.Document.Blocks.Remove(firstParagraph);
                }
            }
        }
    }
}
