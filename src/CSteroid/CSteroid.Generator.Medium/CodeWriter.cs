using System.Text;

namespace CSteroid.Generator.Medium
{
    internal sealed class CodeWriter
    {
        private readonly StringBuilder _builder = new StringBuilder();
        private int _indent;

        internal void AppendLine(string text = "")
        {
            if (text.Length != 0)
                _builder.Append(' ', _indent * 4).Append(text);
            _builder.Append('\n');
        }

        internal void OpenBlock(string text)
        {
            AppendLine(text);
            AppendLine("{");
            _indent++;
        }

        internal void CloseBlock(string suffix = "")
        {
            _indent--;
            AppendLine("}" + suffix);
        }

        public override string ToString() => _builder.ToString();
    }
}
