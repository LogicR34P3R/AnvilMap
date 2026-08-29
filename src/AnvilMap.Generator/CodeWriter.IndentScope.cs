using System;

namespace AnvilMap.Generator;

internal sealed partial class CodeWriter
{
    private sealed class IndentScope : IDisposable
    {
        private readonly CodeWriter _writer;
        private readonly bool _writeClosingBrace;
        private readonly string _closeSuffix;
        private bool _disposed;

        public IndentScope(CodeWriter writer, bool writeClosingBrace, string closeSuffix)
        {
            _writer = writer;
            _writeClosingBrace = writeClosingBrace;
            _closeSuffix = closeSuffix;
            _writer._indent++;
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer._indent--;

            if (_writeClosingBrace)
            {
                _writer.WriteLine("}" + _closeSuffix);
            }
        }
    }
}
