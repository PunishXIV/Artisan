using Serilog.Events;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ECommons.Logging
{
    public record struct InternalLogMessage
    {
        public string Message;
        public LogEventLevel Level;

        public InternalLogMessage(string Message, LogEventLevel Level = LogEventLevel.Information)
        {
            this.Message = Message;
            this.Level = Level;
        }
    }
}
