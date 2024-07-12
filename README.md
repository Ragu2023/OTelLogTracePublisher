# OTelLogTracePublisher
A simple console app to publish Otel logs and traces.
Create .Net8.0 C#.Net blank application and name it as "EventGenerator".

Replace the Content from Program.cs

Add EventGeneratorJsonSerializerContext.cs to the solution.

Build the Application.

Various ways to run the application:

1) Start Event Generator to publish a log and a span event. Executing this command will publish a log and a span event to the port 4317.
  EventGenerator.exe --e  http://localhost:4317;http://localhost:4317 --r 1

2) Start Event Generator to publish only log event.
  EventGenerator.exe --e  http://localhost:4317 --r 1 --p 1

3) Start Event Generator to publish only span/trace event.
  EventGenerator.exe --e  http://localhost:4317 --r 1 --p 2

4) Start Event Generator to publish a logs and trace event continuously for 500 times with the interval of 1 second.
  EventGenerator.exe --e  http://localhost:4317 --r 1 --p 2 --i 500 --b 1000
