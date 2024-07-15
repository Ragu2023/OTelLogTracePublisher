using CommandLine;
using CommandLine.Text;

namespace EventGenerator
{
    public static class CommandLineArgumentResolver
    {
        private static readonly Parser Parser;

        static CommandLineArgumentResolver()
        {
            CommandLineArgumentResolver.Parser = new Parser((config) =>
            {
                config.EnableDashDash = true;
                config.AutoHelp = false;
                config.IgnoreUnknownArguments = true;
            });
        }

        public static T Resolve<T>(string[] args) where T : new()
        {
            T ret = new T();
            ParserResult<T> result = null;
            result = CommandLineArgumentResolver.Parser.ParseArguments<T>(args);

            result.WithParsed<T>(parsed =>
            {
                ret = parsed;
            })
            .WithNotParsed<T>(errs =>
            {
                CommandLineArgumentResolver.DisplayHelp<T>(result, errs);
            });

            return ret;
        }

        public static string SerializeCommandLineArguments<T>(T commandLineArguments, bool enableDashDash)
        {
            Parser parser = new Parser((config) =>
            {
                config.EnableDashDash = enableDashDash;
                config.AutoHelp = false;
                config.IgnoreUnknownArguments = true;
            });

            return UnParserExtensions.FormatCommandLine<T>(
                parser,
                commandLineArguments,
                (unparserConfig) =>
                {
                    unparserConfig.PreferShortName = true;
                });
        }

        private static void DisplayHelp<T>(ParserResult<T> result, IEnumerable<CommandLine.Error> errs)
        {
            HelpText helpText = null;
            if (errs.IsVersion())
            {
                helpText = HelpText.AutoBuild(result);
            }
            else
            {
                helpText = HelpText.AutoBuild(
                    result,
                    h =>
                    {
                        h.AddDashesToOption = true;
                        h.AdditionalNewLineAfterOption = false;
                        //TODO: Set the real version
                        h.Heading = "PipelineAgent v0.1b";
                        h.Copyright = "Copyright (c) Microsoft";
                        return HelpText.DefaultParsingErrorsHandler(result, h);
                    },
                    e => e);
            }

            Console.WriteLine(helpText);
        }
    }
}
