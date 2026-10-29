using System;

namespace XRLumina.Core.API
{
    /// <summary>호스트가 내려준 세션 정보.</summary>
    [Serializable]
    public sealed class SessionData
    {
        public string uuid;
        public int seq;
    }
}
