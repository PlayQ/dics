using System;
using System.Runtime.Serialization;

namespace DICS
{
    [Serializable]
    public abstract class DicsException : Exception
    {
        public DicsException()
        {
        }

        public DicsException(string message)
            : base(message)
        {
        }

        public DicsException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DicsException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class DicsBug : DicsException
    {
        public DicsBug()
        {
        }

        public DicsBug(string message)
            : base(message)
        {
        }

        public DicsBug(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DicsBug(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class DicsRuntimeException : DicsException
    {
        public DicsRuntimeException()
        {
        }

        public DicsRuntimeException(string message)
            : base(message)
        {
        }

        public DicsRuntimeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DicsRuntimeException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class DicsPlanningException : DicsException
    {
        public DicsPlanningException()
        {
        }

        public DicsPlanningException(string message)
            : base(message)
        {
        }

        public DicsPlanningException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DicsPlanningException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }

    [Serializable]
    public class DicsProducerException : DicsException
    {
        public DicsProducerException()
        {
        }

        public DicsProducerException(string message)
            : base(message)
        {
        }

        public DicsProducerException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        protected DicsProducerException(SerializationInfo info,
            StreamingContext context)
            : base(info, context)
        {
        }
    }
}