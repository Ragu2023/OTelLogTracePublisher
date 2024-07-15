using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CommandLine;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Logs;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace EventGenerator
{
    public partial class Program
    {
        private const int DefaultEventsPerMinute = 10;
        private const int DefaultIterations = 1;

        private static readonly TimeSpan DefaultDelayBetweenEvents = TimeSpan.Zero;
        private static readonly TimeSpan DefaultDelayBetweenBatches = TimeSpan.FromSeconds(1);

        private static readonly EventGeneratorJsonSerializerContext EventGeneratorJsonSerializerContext;

        static Program()
        {
            Program.EventGeneratorJsonSerializerContext = new EventGeneratorJsonSerializerContext(new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            });
        }

        public static async Task Main(string[] args)
        {
            //EventGenerator.exe --e  http://localhost:4317;http://localhost:4317 --r 2000 --p 3 --i 100 --b 1000

            int eventsPerBatch;
            TimeSpan delayBetweenEvents;
            TimeSpan delayBetweenBatches;
            int iterations;

            EventGeneratorArguments cla = CommandLineArgumentResolver.Resolve<EventGeneratorArguments>(args);

            eventsPerBatch = cla.EventsPerBatch;
            if (eventsPerBatch <= 0)
            {
                eventsPerBatch = Program.DefaultEventsPerMinute;
            }

            delayBetweenEvents = TimeSpan.FromMilliseconds(cla.DelayBetweenEvents);
            if (delayBetweenEvents <= TimeSpan.Zero)
            {
                delayBetweenEvents = Program.DefaultDelayBetweenEvents;
            }

            delayBetweenBatches = TimeSpan.FromMilliseconds(cla.DelayBetweenBatches);
            if (delayBetweenBatches <= TimeSpan.Zero)
            {
                delayBetweenBatches = Program.DefaultDelayBetweenBatches;
            }

            iterations = cla.Iterations;
            if (iterations <= 0)
            {
                iterations = Program.DefaultIterations;
            }

            string logEndpoint = cla.Endpoint ?? "http://localhost:4321";
            string traceEndpoint = cla.Endpoint ?? "http://localhost:4321";

            if (!string.IsNullOrEmpty(cla.Endpoint) && cla.Endpoint.Contains(';'))
            {
                logEndpoint = cla.Endpoint.Split(';')[0];
                traceEndpoint = cla.Endpoint.Split(';')[1];
            }

            List<Task> taskList = new List<Task>();

            //pulbish only logs
            if (cla.PublishLogAndTrace != 2)
            {
                Console.WriteLine($"Generating Log events... EventsPerBatch: {eventsPerBatch} DelayBetweenEvents: {delayBetweenEvents} DelayBetweenBatches: {delayBetweenBatches} Iterations: {iterations} Wait: {cla.Wait} Endpoint: {logEndpoint}");
                taskList.Add(Program.SendOTLPLogEvents(eventsPerBatch, delayBetweenEvents, delayBetweenBatches, iterations, cla.Wait, logEndpoint));
            }

            //pulbish only trace
            if (cla.PublishLogAndTrace != 1)
            {
                Console.WriteLine($"Generating Trace events... EventsPerBatch: {eventsPerBatch} DelayBetweenEvents: {delayBetweenEvents} DelayBetweenBatches: {delayBetweenBatches} Iterations: {iterations} Wait: {cla.Wait} Endpoint: {traceEndpoint}");
                taskList.Add(Program.SendOTLPTraceEvents(eventsPerBatch, delayBetweenEvents, delayBetweenBatches, iterations, cla.Wait, traceEndpoint));
            }

            await Task.WhenAll(taskList.ToArray());
        }

        private static async Task SendOTLPTraceEvents(int count, TimeSpan delayBetweenEvents, TimeSpan delayBetweenBatches, int iterations, bool wait, string endpoint)
        {
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

            OtlpExporterOptions opt = new OtlpExporterOptions();
            opt.Endpoint = new Uri(endpoint);
            using (TracerProvider openTelemetry = Sdk.CreateTracerProviderBuilder()
                    .AddSource("MyCompany.MyProduct.MyLibrary")
                    .ConfigureResource(r => r.AddAttributes(new List<KeyValuePair<string, object>>
                    {
                        new KeyValuePair<string, object>("resource-attribute1", "v1"),
                        new KeyValuePair<string, object>("resource-attribute2", "v2"),
                    }))
                    .AddProcessor(
                        new BatchActivityExportProcessor(
                            new OtlpTraceExporter(opt),
                            50000000))
                    .Build())
            {

                //Make sure we start in a full minute boundary
                DateTime now = DateTime.UtcNow;
                DateTime nextMinute = now.TruncateToMinute().AddMinutes(1);

                TimeSpan delay = TimeSpan.FromMilliseconds(((nextMinute - now).TotalMilliseconds + 1));

                if (wait)
                {
                    await Program.SmartDelayAsync(delay);
                }

                for (int j = 0; j < iterations; j++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        using (ActivitySource source = new ActivitySource("MyCompany.MyProduct.MyLibrary", "1.0.0"))
                        using (Activity childSpan = source.StartActivity("SleepSpan", ActivityKind.Consumer))
                        {
                            childSpan?.AddTag("mykey", "myvalue");

                            await Program.SmartDelayAsync(delayBetweenEvents);

                            childSpan.Stop();
                        }
                    }

                    await Program.SmartDelayAsync(delayBetweenBatches);
                }
            }
        }

        private static async Task SendOTLPLogEvents(int count, TimeSpan delayBetweenEvents, TimeSpan delayBetweenBatches, int iterations, bool wait, string endpoint)
        {
            try
            {
                ResourceBuilder resourceBuilder = ResourceBuilder.CreateDefault().AddService(serviceName: "MyCompany.MyProduct.EventGeneratorLogTest", serviceVersion: "1.0.0")
                    // add attributes for the OpenTelemetry SDK version
                    //.AddTelemetrySdk()
                    // add custom attributes
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["host"] = Environment.MachineName,
                        ["os"] = RuntimeInformation.OSDescription,
                    });

                using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
                {
                    builder.AddOpenTelemetry((opt) =>
                    {
                        opt.SetResourceBuilder(resourceBuilder);
                        opt.IncludeFormattedMessage = true;
                        opt.IncludeScopes = true;
                        opt.ParseStateValues = true;
                        opt.AddOtlpExporter((exporterOptions, processorOptions) =>
                        {
                            exporterOptions.Protocol = OtlpExportProtocol.Grpc;
                            exporterOptions.Endpoint = new Uri(endpoint);

                            processorOptions.ExportProcessorType = ExportProcessorType.Batch;
                            processorOptions.BatchExportProcessorOptions = new BatchExportLogRecordProcessorOptions()
                            {
                                MaxQueueSize = 1000000,
                                MaxExportBatchSize = 10000,
                                ScheduledDelayMilliseconds = 1000,
                                ExporterTimeoutMilliseconds = 10000,
                            };
                        });
                    });
                });

                //Make sure we start in a full minute boundary
                DateTime now = DateTime.UtcNow;
                DateTime nextMinute = now.TruncateToMinute().AddMinutes(1);

                TimeSpan delay = TimeSpan.FromMilliseconds(((nextMinute - now).TotalMilliseconds + 1));

                if (wait)
                {
                    await Program.SmartDelayAsync(delay);
                }

                ILogger logger = loggerFactory.CreateLogger("Log");
                for (int j = 0; j < iterations; j++)
                {
                    using (logger.BeginScope("{string.attribute}", "some string"))
                    using (logger.BeginScope("{boolean.attribute}", true))
                    using (logger.BeginScope("{boolean.attribute}", true))
                    using (logger.BeginScope("{int.attribute}", 10))
                    using (logger.BeginScope("{double.attribute}", 11.11))
                    using (logger.BeginScope("{array.attribute}", JsonSerializer.Serialize(new string[] { "arr1", "arr2" }, typeof(string[]), Program.EventGeneratorJsonSerializerContext)))
                    using (logger.BeginScope("{map.attribute}", JsonSerializer.Serialize(new Dictionary<string, string>() { { "some.map.key", "some value" } }, typeof(Dictionary<string, string>), Program.EventGeneratorJsonSerializerContext)))
                    {
                        for (int i = 0; i < count; i++)
                        {
                            logger.LogInformation("Example log record {Iteration} {Count}.", j, i);

                            await Program.SmartDelayAsync(delayBetweenEvents);
                        }
                    }

                    await Program.SmartDelayAsync(delayBetweenBatches);
                }

                await Program.SmartDelayAsync(TimeSpan.FromSeconds(10));
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static async Task SmartDelayAsync(TimeSpan delay)
        {
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            //Windows thread scheduling quantum is 16ms
            if (delay > TimeSpan.FromMilliseconds(16))
            {
                await Task.Delay(delay);
            }
            else
            {
                Thread.Sleep(delay);
            }
        }

        internal class EventGeneratorArguments
        {
            [Option('r', "eventsperbatch", Required = false, HelpText = "Number of events to generate per batch.")]
            public int EventsPerBatch { get; set; }

            [Option('d', "delaybetweenevents", Required = false, HelpText = "Delay between each message in a batch in milliseconds.")]
            public int DelayBetweenEvents { get; set; }

            [Option('b', "delaybetweenbatches", Required = false, HelpText = "Delay between each batch in milliseconds.")]
            public int DelayBetweenBatches { get; set; }

            [Option('i', "iterations", Required = false, HelpText = "Number of batches to generate.")]
            public int Iterations { get; set; }

            [Option('w', "wait", Required = false, HelpText = "Wait for the start of a minute before producing events.")]
            public bool Wait { get; set; }

            [Option('e', "endpoint", Required = false, HelpText = "Provide the endpoint with the port, example: http://localhost:4321.")]
            public string Endpoint { get; set; }

            [Option('p', "publishlogandtrace", Required = false, HelpText = "Publish Logs and Traces. Input: 1 -> only logs, 2 -> only traces, 3 -> logs and traces (default).")]
            public int PublishLogAndTrace { get; set; }
        }
    }
}
