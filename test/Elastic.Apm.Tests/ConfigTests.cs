﻿using System;
using System.Linq;
using System.Threading;
using Elastic.Apm.Config;
using Elastic.Apm.Logging;
using Elastic.Apm.Tests.Mocks;
using FluentAssertions;
using Xunit;
using static Elastic.Apm.Config.ConfigConsts;

namespace Elastic.Apm.Tests
{
	/// <summary>
	/// Tests the configuration through environment variables
	/// </summary>
	public class ConfigTests : IDisposable
	{
		[Fact]
		public void ServerUrlsSimpleTest()
		{
			var serverUrl = "http://myServer.com:1234";
			var agent = new ApmAgent(new TestAgentComponents(configurationReader: new TestAgentConfigurationReader(serverUrls: serverUrl)));
			agent.ConfigurationReader.ServerUrls[0].OriginalString.Should().Be(serverUrl);
			var rootedUrl = serverUrl + "/";
			rootedUrl.Should().BeEquivalentTo(agent.ConfigurationReader.ServerUrls[0].AbsoluteUri);
		}

		[Fact]
		public void ServerUrlsInvalidUrlTest()
		{
			var serverUrl = "InvalidUrl";
			var agent = new ApmAgent(new TestAgentComponents(configurationReader: new TestAgentConfigurationReader(serverUrls: serverUrl)));
			agent.ConfigurationReader.ServerUrls[0].Should().Be(DefaultValues.ServerUri);
		}

		[Fact]
		public void ServerUrlInvalidUrlLogTest()
		{
			var serverUrl = "InvalidUrl";
			var logger = new TestLogger();
			var agent = new ApmAgent(new TestAgentComponents(logger,
				new TestAgentConfigurationReader(logger, serverUrls: serverUrl)));
			agent.ConfigurationReader.ServerUrls[0].Should().Be(DefaultValues.ServerUri);

			logger.Lines.Should().NotBeEmpty();
			logger.Lines[0]
				.Should()
				.ContainAll(
					$"{{{nameof(TestAgentConfigurationReader)}}}",
					"Failed parsing server URL from",
					TestAgentConfigurationReader.Origin,
					EnvVarNames.ServerUrls,
					serverUrl
				);
		}

		/// <summary>
		/// Sets 2 servers and makes sure that they are all parsed
		/// </summary>
		[Fact]
		public void ServerUrlsMultipleUrlsTest()
		{
			var serverUrl1 = "http://myServer1.com:1234";
			var serverUrl2 = "http://myServer2.com:1234";
			var serverUrls = $"{serverUrl1},{serverUrl2}";

			var logger = new TestLogger();
			var agent = new ApmAgent(new TestAgentComponents(logger,
				new TestAgentConfigurationReader(logger, serverUrls: serverUrls)));

			var parsedUrls = agent.ConfigurationReader.ServerUrls;
			parsedUrls[0].OriginalString.Should().Be(serverUrl1);
			parsedUrls[0].AbsoluteUri.Should().BeEquivalentTo($"{serverUrl1}/");

			parsedUrls[1].OriginalString.Should().Be(serverUrl2);
			parsedUrls[1].AbsoluteUri.Should().BeEquivalentTo($"{serverUrl2}/");
		}

		/// <summary>
		/// Sets 3 server urls, 2 of them are valid, 1 is invalid
		/// Makes sure that the 2 valid urls are parsed and there is a log line for the invalid server url
		/// </summary>
		[Fact]
		public void ServerUrlsMultipleUrlsWith1InvalidUrlTest()
		{
			var serverUrl1 = "http://myServer1.com:1234";
			var serverUrl2 = "invalidUrl";
			var serverUrl3 = "http://myServer2.com:1234";
			var serverUrls = $"{serverUrl1},{serverUrl2},{serverUrl3}";
			var logger = new TestLogger();
			var agent = new ApmAgent(new TestAgentComponents(logger,
				new TestAgentConfigurationReader(logger, serverUrls: serverUrls)));

			var parsedUrls = agent.ConfigurationReader.ServerUrls;
			parsedUrls.Should().NotBeEmpty().And.HaveCount(2, "seeded 3 but one was invalid");
			parsedUrls[0].OriginalString.Should().Be(serverUrl1);
			parsedUrls[0].AbsoluteUri.Should().BeEquivalentTo($"{serverUrl1}/");

			parsedUrls[1].OriginalString.Should().Be(serverUrl3);
			parsedUrls[1].AbsoluteUri.Should().BeEquivalentTo($"{serverUrl3}/");

			logger.Lines.Should().NotBeEmpty();
			logger.Lines[0]
				.Should()
				.ContainAll(
					$"{{{nameof(TestAgentConfigurationReader)}}}",
					"Failed parsing server URL from",
					TestAgentConfigurationReader.Origin,
					EnvVarNames.ServerUrls,
					serverUrl2
				);
		}

		/// <summary>
		/// Makes sure empty spaces are trimmed at the end of the config
		/// </summary>
		[Fact]
		public void ReadServerUrlsWithSpaceAtTheEndViaEnvironmentVariable()
		{
			var serverUrlsWithSpace = "http://myServer:1234 \r\n";
			Environment.SetEnvironmentVariable(EnvVarNames.ServerUrls, serverUrlsWithSpace);
			var payloadSender = new MockPayloadSender();
			using (var agent = new ApmAgent(new AgentComponents(payloadSender: payloadSender)))
			{
				agent.ConfigurationReader.ServerUrls.First().Should().NotBe(serverUrlsWithSpace);
				agent.ConfigurationReader.ServerUrls.First().Should().Be("http://myServer:1234");
			}
		}

		[Fact]
		public void SecretTokenSimpleTest()
		{
			var secretToken = "secretToken";
			var agent = new ApmAgent(new TestAgentComponents(configurationReader: new TestAgentConfigurationReader(secretToken: secretToken)));
			agent.ConfigurationReader.SecretToken.Should().Be(secretToken);
		}

		[Fact]
		public void DefaultCaptureHeadersTest()
		{
			using (var agent = new ApmAgent(new TestAgentComponents())) agent.ConfigurationReader.CaptureHeaders.Should().Be(true);
		}

		[Fact]
		public void SetCaptureHeadersTest()
		{
			Environment.SetEnvironmentVariable(EnvVarNames.CaptureHeaders, "false");
			var config = new EnvironmentConfigurationReader();
			config.CaptureHeaders.Should().Be(false);
		}

		[Fact]
		public void DefaultTransactionSampleRateTest()
		{
			using (var agent = new ApmAgent(new TestAgentComponents()))
				agent.ConfigurationReader.TransactionSampleRate.Should().Be(DefaultValues.TransactionSampleRate);
		}

		[Fact]
		public void SetTransactionSampleRateTest()
		{
			Environment.SetEnvironmentVariable(EnvVarNames.TransactionSampleRate, "0.789");
			var config = new EnvironmentConfigurationReader();
			config.TransactionSampleRate.Should().Be(0.789);
		}

		[Fact]
		public void TransactionSampleRateExpectsDotForFloatingPoint()
		{
			Environment.SetEnvironmentVariable(EnvVarNames.TransactionSampleRate, "0,789");
			var config = new EnvironmentConfigurationReader();
			// Since comma was used instead of dot then default value will be used
			config.TransactionSampleRate.Should().Be(DefaultValues.TransactionSampleRate);
		}

		[Fact]
		public void DefaultLogLevelTest() => Agent.Config.LogLevel.Should().Be(LogLevel.Error);

		[Theory]
		[InlineData("Trace", LogLevel.Trace)]
		[InlineData("Debug", LogLevel.Debug)]
		[InlineData("Information", LogLevel.Information)]
		[InlineData("Warning", LogLevel.Warning)]
		[InlineData("Error", LogLevel.Error)]
		[InlineData("Critical", LogLevel.Critical)]
		public void SetLogLevelTest(string logLevelAsString, LogLevel logLevel)
		{
			var logger = new TestLogger(logLevel);
			var agent = new ApmAgent(new TestAgentComponents(logger, new TestAgentConfigurationReader(logger, logLevelAsString)));
			agent.ConfigurationReader.LogLevel.Should().Be(logLevel);
			agent.Logger.Should().Be(logger);
			foreach (LogLevel enumValue in Enum.GetValues(typeof(LogLevel)))
			{
				if (logLevel <= enumValue)
					agent.Logger.IsEnabled(enumValue).Should().BeTrue();
				else
					agent.Logger.IsEnabled(enumValue).Should().BeFalse();
			}
		}

		[Fact]
		public void SetInvalidLogLevelTest()
		{
			var logger = new TestLogger(LogLevel.Error);
			var logLevelAsString = "InvalidLogLevel";
			var agent = new ApmAgent(new TestAgentComponents(logger, new TestAgentConfigurationReader(logger, logLevelAsString)));

			agent.ConfigurationReader.LogLevel.Should().Be(LogLevel.Error);
			logger.Lines.Should().NotBeEmpty();
			logger.Lines[0]
				.Should()
				.ContainAll(
					$"{{{nameof(TestAgentConfigurationReader)}}}",
					"Failed parsing log level from",
					TestAgentConfigurationReader.Origin,
					EnvVarNames.LogLevel,
					"Defaulting to "
				);
		}

		/// <summary>
		/// The server doesn't accept services with '.' in it.
		/// This test makes sure we don't have '.' in the default service name.
		/// </summary>
		[Fact]
		public void DefaultServiceNameTest()
		{
			var payloadSender = new MockPayloadSender();
			var agent = new ApmAgent(new AgentComponents(payloadSender: payloadSender));
			agent.Tracer.CaptureTransaction("TestTransactionName", "TestTransactionType", t => { Thread.Sleep(2); });

			//By default XUnit uses 'testhost' as the entry assembly, and that is what the
			//agent reports if we don't set it to anything:
			var serviceName = agent.Service.Name;
			serviceName.Should().NotBeNullOrWhiteSpace();
			serviceName.Should().NotContain(".");
		}

		/// <summary>
		/// Sets the ELASTIC_APM_SERVICE_NAME environment variable and makes sure that
		/// when the agent sends data to the server it has the value from the
		/// ELASTIC_APM_SERVICE_NAME environment variable as service name.
		/// </summary>
		[Fact]
		public void ReadServiceNameViaEnvironmentVariable()
		{
			var serviceName = "MyService123";
			Environment.SetEnvironmentVariable(EnvVarNames.ServiceName, serviceName);
			var payloadSender = new MockPayloadSender();
			var agent = new ApmAgent(new AgentComponents(payloadSender: payloadSender));
			agent.Tracer.CaptureTransaction("TestTransactionName", "TestTransactionType", t => { Thread.Sleep(2); });

			agent.Service.Name.Should().Be(serviceName);
		}

		/// <summary>
		/// Sets the ELASTIC_APM_SERVICE_NAME environment variable to a value that contains a '.'
		/// Makes sure that when the agent sends data to the server it has the value from the
		/// ELASTIC_APM_SERVICE_NAME environment variable as service name and also makes sure that
		/// the '.' is replaced.
		/// </summary>
		[Fact]
		public void ReadServiceNameWithDotViaEnvironmentVariable()
		{
			var serviceName = "My.Service.Test";
			Environment.SetEnvironmentVariable(EnvVarNames.ServiceName, serviceName);
			var payloadSender = new MockPayloadSender();
			var agent = new ApmAgent(new AgentComponents(payloadSender: payloadSender));
			agent.Tracer.CaptureTransaction("TestTransactionName", "TestTransactionType", t => { Thread.Sleep(2); });


			agent.Service.Name.Should().Be(serviceName.Replace('.', '_'));
			agent.Service.Name.Should().NotContain(".");
		}

		/// <summary>
		/// The test makes sure we validate service name.
		/// </summary>
		[Fact]
		public void ReadInvalidServiceNameViaEnvironmentVariable()
		{
			var serviceName = "MyService123!";
			Environment.SetEnvironmentVariable(EnvVarNames.ServiceName, serviceName);
			var payloadSender = new MockPayloadSender();
			var agent = new ApmAgent(new AgentComponents(payloadSender: payloadSender));
			agent.Tracer.CaptureTransaction("TestTransactionName", "TestTransactionType", t => { Thread.Sleep(2); });

			agent.Service.Name.Should().NotBe(serviceName);
			agent.Service.Name.Should().MatchRegex("^[a-zA-Z0-9 _-]+$")
				.And.Be("MyService123_");
		}

		/// <summary>
		/// The test makes sure that unknown service name value fits to all constraints.
		/// </summary>
		[Fact]
		public void UnknownServiceNameValueTest()
		{
			var serviceName = DefaultValues.UnknownServiceName;
			Environment.SetEnvironmentVariable(EnvVarNames.ServiceName, serviceName);
			var payloadSender = new MockPayloadSender();
			var agent = new ApmAgent(new AgentComponents(payloadSender: payloadSender));
			agent.Tracer.CaptureTransaction("TestTransactionName", "TestTransactionType", t => { Thread.Sleep(2); });

			agent.Service.Name.Should().Be(serviceName);
			agent.Service.Name.Should().MatchRegex("^[a-zA-Z0-9 _-]+$");
		}

		/// <summary>
		/// In case the user does not provide us a service name we try to calculate it based on the callstack.
		/// This test makes sure we recognize mscorlib and our own assemblies correctly in the
		/// <see cref="AbstractConfigurationReader.IsMsOrElastic(byte[])" /> method.
		/// </summary>
		[Fact]
		public void TestAbstractConfigurationReaderIsMsOrElastic()
		{
			var elasticToken = new byte[] { 174, 116, 0, 210, 193, 137, 207, 34 };
			var mscorlibToken = new byte[] { 183, 122, 92, 86, 25, 52, 224, 137 };

			AbstractConfigurationReader.IsMsOrElastic(elasticToken).Should().BeTrue();

			AbstractConfigurationReader.IsMsOrElastic(new byte[] { 0 }).Should().BeFalse();
			AbstractConfigurationReader.IsMsOrElastic(new byte[] { }).Should().BeFalse();

			AbstractConfigurationReader
				.IsMsOrElastic(new[]
				{
					elasticToken[0], mscorlibToken[1], elasticToken[2], mscorlibToken[3], elasticToken[4], mscorlibToken[5], elasticToken[6],
					mscorlibToken[7]
				})
				.Should()
				.BeFalse();
		}

		/// <summary>
		/// Makes sure that the <see cref="EnvironmentConfigurationReader" /> logs
		/// in case it reads an invalid URL.
		/// </summary>
		[Fact]
		public void LoggerNotNull()
		{
			Environment.SetEnvironmentVariable(EnvVarNames.ServerUrls, "localhost"); //invalid, it should be "http://localhost"
			var testLogger = new TestLogger();
			var config = new EnvironmentConfigurationReader(testLogger);
			var serverUrl = config.ServerUrls.FirstOrDefault();

			serverUrl.Should().NotBeNull();
			testLogger.Lines.Should().NotBeEmpty();
		}

		[Fact]
		public void SetMetricsIntervalTo10S()
			=> MetricsIntervalTestCommon("10s").Should().Be(10 * 1000);

		/// <summary>
		/// Sets the metrics interval to '500ms'
		/// Makes sure that 500ms defaults to 0, since the minimum is 1s
		/// </summary>
		[Fact]
		public void SetMetricsIntervalTo500Ms()
			=> MetricsIntervalTestCommon("500ms").Should().Be(0);

		[Fact]
		public void SetMetricsIntervalTo1500Ms()
			=> MetricsIntervalTestCommon("1500ms").Should().Be(1500);

		[Fact]
		public void SetMetricsIntervalTo1HourAs60Minutes()
			=> MetricsIntervalTestCommon("60m").Should().Be(60 * 60 * 1000);

		[Fact]
		public void SetMetricsIntervalTo1HourUsingUnsupportedUnits()
			=> MetricsIntervalTestCommon("1h").Should().Be(DefaultValues.MetricsIntervalInMilliseconds);

		[Fact]
		public void SetMetricsIntervalTo1M()
			=> MetricsIntervalTestCommon("1m").Should().Be(60 * 1000);

		/// <summary>
		/// Sets the metrics interval to '10'.
		/// Makes sure that '10' defaults to '10s'
		/// </summary>
		[Fact]
		public void SetMetricsIntervalTo10()
			=> MetricsIntervalTestCommon("10").Should().Be(10 * 1000);

		/// <summary>
		/// Any negative value should be treated as 0
		/// </summary>
		[Fact]
		public void SetMetricsIntervalToNegativeNoUnits()
			=> MetricsIntervalTestCommon("-1").Should().Be(0);

		[Fact]
		public void SetMetricsIntervalToNegativeSeconds()
			=> MetricsIntervalTestCommon("-0.3s").Should().Be(0);

		[Fact]
		public void SetMetricsIntervalToNegativeMinutes()
			=> MetricsIntervalTestCommon("-5m").Should().Be(0);

		[Fact]
		public void SetMetricsIntervalToNegativeMilliseconds()
			=> MetricsIntervalTestCommon("-5ms").Should().Be(0);

		/// <summary>
		/// Make sure <see cref="DefaultValues.MetricsInterval" /> and <see cref="DefaultValues.MetricsIntervalInMilliseconds" />
		/// are in sync
		/// </summary>
		[Fact]
		public void MetricsIntervalDefaultValuesInSync()
			=> MetricsIntervalTestCommon(DefaultValues.MetricsInterval).Should().Be(DefaultValues.MetricsIntervalInMilliseconds);

		[Fact]
		public void SpanFramesMinDurationDefaultValuesInSync()
		{
			Environment.SetEnvironmentVariable(EnvVarNames.MetricsInterval, DefaultValues.SpanFramesMinDuration);
			var testLogger = new TestLogger();
			var config = new EnvironmentConfigurationReader(testLogger);
			config.SpanFramesMinDurationInMilliseconds.Should().Be(DefaultValues.SpanFramesMinDurationInMilliseconds);
		}

		[InlineData("2", 2)]
		[InlineData("0", 0)]
		[InlineData("-2", -2)]
		[InlineData("2147483647", int.MaxValue)]
		[InlineData("-2147483648", int.MinValue)]
		[InlineData("2.32", DefaultValues.StackTraceLimit)]
		[InlineData("2,32", DefaultValues.StackTraceLimit)]
		[InlineData("asdf", DefaultValues.StackTraceLimit)]
		[Theory]
		public void StackTraceLimit(string configValue, int expectedValue)
		{
			using (var agent =
				new ApmAgent(new TestAgentComponents(configurationReader: new TestAgentConfigurationReader(stackTraceLimit: configValue))))
				agent.ConfigurationReader.StackTraceLimit.Should().Be(expectedValue);
		}

		[InlineData("2ms", 2)]
		[InlineData("2s", 2 * 1000)]
		[InlineData("2m", 2 * 60 * 1000)]
		[InlineData("2", 2)]
		[InlineData("-2ms", -2)]
		[InlineData("dsfkldfs", DefaultValues.SpanFramesMinDurationInMilliseconds)]
		[InlineData("2,32", DefaultValues.SpanFramesMinDurationInMilliseconds)]
		[Theory]
		public void SpanFramesMinDurationInMilliseconds(string configValue, int expectedValue)
		{
			using (var agent =
				new ApmAgent(new TestAgentComponents(
					configurationReader: new TestAgentConfigurationReader(spanFramesMinDurationInMilliseconds: configValue))))
				agent.ConfigurationReader.SpanFramesMinDurationInMilliseconds.Should().Be(expectedValue);
		}

		private static double MetricsIntervalTestCommon(string configValue)
		{
			Environment.SetEnvironmentVariable(EnvVarNames.MetricsInterval, configValue);
			var testLogger = new TestLogger();
			var config = new EnvironmentConfigurationReader(testLogger);
			return config.MetricsIntervalInMilliseconds;
		}

		public void Dispose()
		{
			Environment.SetEnvironmentVariable(EnvVarNames.ServerUrls, null);
			Environment.SetEnvironmentVariable(EnvVarNames.MetricsInterval, null);
		}
	}
}
