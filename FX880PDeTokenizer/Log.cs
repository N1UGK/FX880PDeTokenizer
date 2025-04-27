using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NLog.Config;
using NLog.Targets;

namespace FX880PDeTokenizer
{
    public class Log
    {
        public Logger logger;

        public Log()
        {
            // Step 1. Create configuration object 
            var config = new LoggingConfiguration();

            // Step 2. Create targets and add them to the configuration 
            var consoleTarget = new ColoredConsoleTarget();

            config.AddTarget("console", consoleTarget);

            var fileTarget = new FileTarget();

            config.AddTarget("file", fileTarget);

            // Step 3. Set target properties 
            consoleTarget.Layout = @"${date:format=yyyy\-MM\-dd HH\:mm\:ss.fff} ${message}";

            fileTarget.FileName = "${basedir}/log.txt";
            fileTarget.Layout = @"${date:format=yyyy\-MM\-dd HH\:mm\:ss.fff} ${message}";

            // Step 4. Define rules
            var rule1 = new LoggingRule("*", LogLevel.Debug, consoleTarget);
            config.LoggingRules.Add(rule1);

            var rule2 = new LoggingRule("*", LogLevel.Debug, fileTarget);
            config.LoggingRules.Add(rule2);

            // Step 5. Activate the configuration
            LogManager.Configuration = config;

            // Example usage
            logger = LogManager.GetLogger(string.Empty);
        }
    }
}
