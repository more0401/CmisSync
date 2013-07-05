using System;
using System.Runtime.Serialization;

namespace CmisSync.Lib.Cmis
{
    [Serializable]
    public class CmisPermissionDeniedException : Exception
    {
        public CmisPermissionDeniedException() { }
        public CmisPermissionDeniedException(string message) : base(message) { }
        public CmisPermissionDeniedException(string message, Exception inner) : base(message, inner) { }
        protected CmisPermissionDeniedException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
