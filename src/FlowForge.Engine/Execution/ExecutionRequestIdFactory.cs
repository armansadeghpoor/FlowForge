using System.Security.Cryptography;
using System.Text;
using FlowForge.Core.Domain.Identifiers;

namespace FlowForge.Engine.Execution;

internal static class ExecutionRequestIdFactory
{
    public static ExecutionRequestId Create(Guid scope, string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var scopeBytes = scope.ToByteArray();
        var valueBytes = Encoding.UTF8.GetBytes(value);
        var input = new byte[scopeBytes.Length + valueBytes.Length];
        Buffer.BlockCopy(scopeBytes, 0, input, 0, scopeBytes.Length);
        Buffer.BlockCopy(valueBytes, 0, input, scopeBytes.Length, valueBytes.Length);

        var hash = SHA256.HashData(input);
        return new ExecutionRequestId(new Guid(hash.AsSpan(0, 16)));
    }
}
