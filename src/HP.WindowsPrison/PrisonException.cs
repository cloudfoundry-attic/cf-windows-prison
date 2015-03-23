namespace HP.WindowsPrison
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using System.Linq;
    using System.Runtime.Serialization;
    using System.Text;
    using System.Threading.Tasks;

    /// <summary>
    /// This is an exception type that can be raised by the Windows Prison library.
    /// </summary>
    [Serializable]
    public class PrisonException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the PrisonException class. Inherited from the Exception class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="exception">Inner exception</param>
        public PrisonException(string message, Exception exception)
            : base(message, exception)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PrisonException class. Inherited from the Exception class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        /// <param name="formatArgs">Format arguments.</param>
        public PrisonException(string message, params object[] formatArgs)
            : this(string.Format(CultureInfo.InvariantCulture, message, formatArgs))
        {
        }

        /// <summary>
        /// Initializes a new instance of the PrisonException class. Inherited from the Exception class.
        /// </summary>
        /// <param name="message">Exception message.</param>
        public PrisonException(string message)
            : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the PrisonException class.
        /// </summary>
        public PrisonException()
        {
        }
        
        /// <summary>
        /// Initializes a new instance of the PrisonException class. 
        /// Constructor required by the [Serializable] attribute.
        /// </summary>
        /// <param name="serializationInfo">Serialization info.</param>
        /// <param name="streamingContext">Streaming context.</param>
        protected PrisonException(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
        }
    }
}
