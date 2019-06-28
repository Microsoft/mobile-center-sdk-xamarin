﻿// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using Microsoft.AppCenter.Crashes.Ingestion.Models;
using Microsoft.AppCenter.Crashes.Utils;
using Microsoft.AppCenter.Ingestion.Models.Serialization;
using Microsoft.AppCenter.Utils;
using Microsoft.AppCenter.Utils.Files;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace Microsoft.AppCenter.Crashes.Test.Windows.Utils
{
    [TestClass]
    public class ErrorLogHelperTest
    {
        [TestInitialize]
        public void SetUp()
        {
            ErrorLogHelper.Instance._processInformation = Mock.Of<IProcessInformation>();
            ErrorLogHelper.Instance._deviceInformationHelper = Mock.Of<IDeviceInformationHelper>();
            LogSerializer.AddLogType("managedError", typeof(ManagedErrorLog));
        }

        [TestCleanup]
        public void Cleanup()
        {
            // If a mock was set, reset it to null before moving on.
            ErrorLogHelper.Instance = null;
        }

        [TestMethod]
        public void CreateErrorLog()
        {
            // Set up an exception. This is needed because inner exceptions cannot be mocked.
            System.Exception exception;
            try
            {
                throw new AggregateException("mainException", new System.Exception("innerException1"), new System.Exception("innerException2", new System.Exception("veryInnerException")));
            }
            catch (System.Exception e)
            {
                exception = e;
            }

            // Mock device information.
            var device = new Microsoft.AppCenter.Ingestion.Models.Device("sdkName", "sdkVersion", "osName", "osVersion", "locale", 1,
                "appVersion", "appBuild", null, null, "model", "oemName", "osBuild", null, "screenSize", null, null, "appNamespace", null, null, null, null);
            Mock.Get(ErrorLogHelper.Instance._deviceInformationHelper).Setup(instance => instance.GetDeviceInformation()).Returns(device);

            // Mock process information.
            var parentProcessId = 0;
            var parentProcessName = "parentProcessName";
            var processArchitecture = "processArchitecture";
            var processId = 1;
            var processName = "processName";
            var processStartTime = DateTime.Now;
            Mock.Get(ErrorLogHelper.Instance._processInformation).SetupGet(instance => instance.ParentProcessId).Returns(parentProcessId);
            Mock.Get(ErrorLogHelper.Instance._processInformation).SetupGet(instance => instance.ParentProcessName).Returns(parentProcessName);
            Mock.Get(ErrorLogHelper.Instance._processInformation).SetupGet(instance => instance.ProcessArchitecture).Returns(processArchitecture);
            Mock.Get(ErrorLogHelper.Instance._processInformation).SetupGet(instance => instance.ProcessId).Returns(processId);
            Mock.Get(ErrorLogHelper.Instance._processInformation).SetupGet(instance => instance.ProcessName).Returns(processName);
            Mock.Get(ErrorLogHelper.Instance._processInformation).SetupGet(instance => instance.ProcessStartTime).Returns(processStartTime);

            // Create the error log.
            var log = ErrorLogHelper.CreateErrorLog(exception);

            // Validate the result.
            Assert.AreEqual(exception.StackTrace, log.Exception.StackTrace);
            Assert.AreEqual(exception.Message, log.Exception.Message);
            Assert.AreEqual(3, log.Exception.InnerExceptions.Count, 3);
            Assert.AreEqual((exception as AggregateException).InnerExceptions[0].Message, log.Exception.InnerExceptions[0].Message);
            Assert.AreEqual((exception as AggregateException).InnerExceptions[1].Message, log.Exception.InnerExceptions[1].Message);
            Assert.AreEqual((exception as AggregateException).InnerExceptions[1].InnerException.Message, log.Exception.InnerExceptions[1].InnerExceptions[0].Message);
            Assert.AreEqual(device.SdkName, log.Device.SdkName);
            Assert.AreEqual(device.SdkVersion, log.Device.SdkVersion);
            Assert.AreEqual(device.OsName, log.Device.OsName);
            Assert.AreEqual(device.OsVersion, log.Device.OsVersion);
            Assert.AreEqual(device.Locale, log.Device.Locale);
            Assert.AreEqual(device.TimeZoneOffset, log.Device.TimeZoneOffset);
            Assert.AreEqual(device.AppVersion, log.Device.AppVersion);
            Assert.AreEqual(device.AppBuild, log.Device.AppBuild);
            Assert.AreEqual(device.WrapperSdkVersion, log.Device.WrapperSdkVersion);
            Assert.AreEqual(device.WrapperSdkName, log.Device.WrapperSdkName);
            Assert.AreEqual(device.Model, log.Device.Model);
            Assert.AreEqual(device.OemName, log.Device.OemName);
            Assert.AreEqual(device.OsBuild, log.Device.OsBuild);
            Assert.AreEqual(device.OsApiLevel, log.Device.OsApiLevel);
            Assert.AreEqual(device.ScreenSize, log.Device.ScreenSize);
            Assert.AreEqual(device.CarrierName, log.Device.CarrierName);
            Assert.AreEqual(device.CarrierCountry, log.Device.CarrierCountry);
            Assert.AreEqual(device.AppNamespace, log.Device.AppNamespace);
            Assert.AreEqual(device.LiveUpdateDeploymentKey, log.Device.LiveUpdateDeploymentKey);
            Assert.AreEqual(device.LiveUpdatePackageHash, log.Device.LiveUpdatePackageHash);
            Assert.AreEqual(device.LiveUpdateReleaseLabel, log.Device.LiveUpdateReleaseLabel);
            Assert.AreEqual(device.WrapperRuntimeVersion, log.Device.WrapperRuntimeVersion);
            Assert.AreEqual(parentProcessId, log.ParentProcessId);
            Assert.AreEqual(parentProcessName, log.ParentProcessName);
            Assert.AreEqual(processArchitecture, log.Architecture);
            Assert.AreEqual(processId, log.ProcessId);
            Assert.AreEqual(processName, log.ProcessName);
            Assert.AreEqual(processStartTime, log.AppLaunchTimestamp);
            Assert.IsTrue(log.Fatal);
        }

        [TestMethod]
        public void GetSingleErrorLogFile()
        {
            var id = Guid.NewGuid();
            var expectedFile = Mock.Of<File>();
            var fileList = new List<File> { expectedFile };
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"{id}.json")).Returns(fileList);

            // Retrieve the error log by the ID.
            var errorLogFile = ErrorLogHelper.GetStoredErrorLogFile(id);

            // Validate the contents.
            Assert.AreSame(expectedFile, errorLogFile);
        }

        [TestMethod]
        [DataRow(typeof(System.IO.DirectoryNotFoundException))]
        [DataRow(typeof(SecurityException))]
        public void GetSingleErrorLogFileDoesNotThrow(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles(It.IsAny<string>())).Throws(exception);

            // Retrieve the error log by the ID.
            var errorLogFile = ErrorLogHelper.GetStoredErrorLogFile(Guid.NewGuid());
            Assert.IsNull(errorLogFile);
        }

        [TestMethod]
        public void GetErrorStorageDirectoryCreate()
        {
            // Mock where directory doesn't exist.
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.Create());
            Mock.Get(mockDirectory).Setup(d => d.Exists()).Returns(false);

            var errorStorageDirectory = ErrorLogHelper.GetErrorStorageDirectory();

            // Verify _crashesDirectory was created
            Assert.IsInstanceOfType(errorStorageDirectory, typeof(Directory));
            Mock.Get(mockDirectory).Verify(d => d.Create());
        }

        [TestMethod]
        public void GetErrorLogFiles()
        {
            // Mock multiple error log files.
            var expectedFile1 = Mock.Of<File>();
            var expectedFile2 = Mock.Of<File>();
            var expectedFiles = new List<File> { expectedFile1, expectedFile2 };
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"*.json")).Returns(expectedFiles);

            // Retrieve the error logs.
            var errorLogFiles = ErrorLogHelper.GetErrorLogFiles().ToList();

            // Validate the contents.
            Assert.AreEqual(expectedFiles.Count, errorLogFiles.Count);
            foreach (var fileInfo in errorLogFiles)
            {
                Assert.IsNotNull(fileInfo);
                CollectionAssert.Contains(expectedFiles, fileInfo);
                expectedFiles.Remove(fileInfo);
            }
        }

        [TestMethod]
        [DataRow(typeof(System.IO.DirectoryNotFoundException))]
        [DataRow(typeof(SecurityException))]
        public void GetErrorLogFilesDoesNotThrow(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles(It.IsAny<string>())).Throws(exception);

            // Retrieve the error logs.
            var errorLogFiles = ErrorLogHelper.GetErrorLogFiles();
            Assert.AreEqual(errorLogFiles.Count(), 0);
        }

        [TestMethod]
        public void GetLastErrorLogFile()
        {
            // Mock multiple error log files.
            var oldFile = Mock.Of<File>();
            Mock.Get(oldFile).SetupGet(f => f.LastWriteTime).Returns(DateTime.Now.AddDays(-200));
            var recentFile = Mock.Of<File>();
            Mock.Get(recentFile).SetupGet(f => f.LastWriteTime).Returns(DateTime.Now);
            var expectedFiles = new List<File> { oldFile, recentFile };
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"*.json")).Returns(expectedFiles);

            // Retrieve the error logs.
            var errorLogFile = ErrorLogHelper.GetLastErrorLogFile();

            // Validate the contents.
            Assert.AreSame(recentFile, errorLogFile);
        }

        [TestMethod]
        [DataRow(typeof(System.IO.DirectoryNotFoundException))]
        [DataRow(typeof(SecurityException))]
        public void GetLastErrorLogFileDoesNotThrow(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles(It.IsAny<string>())).Throws(exception);

            // Retrieve the error logs.
            var errorLogFile = ErrorLogHelper.GetLastErrorLogFile();
            Assert.IsNull(errorLogFile);
        }

        [TestMethod]
        [DataRow(typeof(System.IO.IOException))]
        [DataRow(typeof(PlatformNotSupportedException))]
        [DataRow(typeof(ArgumentOutOfRangeException))]
        public void GetLastErrorLogFileDoesNotThrowWhenLastWriteTimeThrows(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;

            // Mock multiple error log files.
            var oldFile = Mock.Of<File>();
            Mock.Get(oldFile).SetupGet(f => f.LastWriteTime).Throws(exception);
            var recentFile = Mock.Of<File>();
            Mock.Get(recentFile).SetupGet(f => f.LastWriteTime).Throws(exception);
            var expectedFiles = new List<File> { oldFile, recentFile };
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"*.json")).Returns(expectedFiles);

            // Retrieve the error logs.
            var errorLogFileInfo = ErrorLogHelper.GetLastErrorLogFile();
            Assert.IsNull(errorLogFileInfo);
        }

        [TestMethod]
        public void GetLastErrorLogFileWhenOnlyOneIsSaved()
        {
            var file = Mock.Of<File>();
            Mock.Get(file).SetupGet(f => f.LastWriteTime).Returns(DateTime.Now);
            var expectedFiles = new List<File> { file };
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"*.json")).Returns(expectedFiles);

            // Retrieve the error logs.
            var errorLogFileInfo = ErrorLogHelper.GetLastErrorLogFile();

            // Validate the contents.
            Assert.AreSame(file, errorLogFileInfo);
        }

        [TestMethod]
        public void ReadErrorLogFile()
        {
            var errorLog = new ManagedErrorLog
            {
                Id = Guid.NewGuid(),
                ProcessId = 123
            };
            var serializedErrorLog = LogSerializer.Serialize(errorLog);
            var mockFile = Mock.Of<File>();
            Mock.Get(mockFile).Setup(file => file.ReadAllText()).Returns(serializedErrorLog);
            var actualContents = ErrorLogHelper.ReadErrorLogFile(mockFile);
            Assert.AreEqual(errorLog.Id, actualContents.Id);
            Assert.AreEqual(errorLog.ProcessId, actualContents.ProcessId);
        }

        [TestMethod]
        public void ReadErrorLogFileThrowsException()
        {
            var mockFile = Mock.Of<File>();
            Mock.Get(mockFile).Setup(file => file.ReadAllText()).Throws(new System.IO.IOException());
            Assert.IsNull(ErrorLogHelper.ReadErrorLogFile(mockFile));
        }

        [TestMethod]
        public void SaveErrorLogFile()
        {
            var errorLog = new ManagedErrorLog
            {
                Id = Guid.NewGuid(),
                ProcessId = 123
            };
            var fileName = errorLog.Id + ".json";
            var serializedErrorLog = LogSerializer.Serialize(errorLog);
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            ErrorLogHelper.SaveErrorLogFile(errorLog);
            Mock.Get(mockDirectory).Verify(d => d.CreateFile(fileName, serializedErrorLog));
        }

        [TestMethod]
        [DataRow(typeof(ArgumentException))]
        [DataRow(typeof(ArgumentNullException))]
        [DataRow(typeof(System.IO.PathTooLongException))]
        [DataRow(typeof(System.IO.DirectoryNotFoundException))]
        [DataRow(typeof(System.IO.IOException))]
        [DataRow(typeof(UnauthorizedAccessException))]
        [DataRow(typeof(NotSupportedException))]
        [DataRow(typeof(SecurityException))]
        public void SaveErrorLogFileDoesNotThrow(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;
            var errorLog = new ManagedErrorLog
            {
                Id = Guid.NewGuid(),
                ProcessId = 123
            };
            var fileName = errorLog.Id + ".json";
            var serializedErrorLog = LogSerializer.Serialize(errorLog);
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles(It.IsAny<string>())).Throws(exception);
            ErrorLogHelper.SaveErrorLogFile(errorLog);

            // No exception should be thrown.
        }

        [TestMethod]
        public void RemoveStoredErrorLogFile()
        {
            var file = Mock.Of<File>();
            var expectedFiles = new List<File> { file };
            var id = Guid.NewGuid();
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"{id}.json")).Returns(expectedFiles);
            ErrorLogHelper.RemoveStoredErrorLogFile(id);
            Mock.Get(file).Verify(f => f.Delete());
        }

        [TestMethod]
        [DataRow(typeof(System.IO.IOException))]
        [DataRow(typeof(SecurityException))]
        [DataRow(typeof(UnauthorizedAccessException))]
        public void RemoveStoredErrorLogFileDoesNotThrow(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;
            var file = Mock.Of<File>();
            Mock.Get(file).Setup(f => f.Delete()).Throws(exception);
            var expectedFiles = new List<File> { file };
            var id = Guid.NewGuid();
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles($"{id}.json")).Returns(expectedFiles);
            ErrorLogHelper.RemoveStoredErrorLogFile(id);
            Mock.Get(file).Verify(f => f.Delete());
            ErrorLogHelper.RemoveStoredErrorLogFile(id);

            // No exception should be thrown.
        }

        [TestMethod]
        public void RemoveAllStoredErrorLogFiles()
        {
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            ErrorLogHelper.RemoveAllStoredErrorLogFiles();
            Mock.Get(mockDirectory).Verify(d => d.Delete(true));
        }

        [TestMethod]
        [DataRow(typeof(System.IO.IOException))]
        [DataRow(typeof(SecurityException))]
        [DataRow(typeof(UnauthorizedAccessException))]
        public void RemoveAllStoredErrorLogFilesDoesNotThrow(Type exceptionType)
        {
            // Use reflection to create an exception of the given C# type.
            var exception = exceptionType.GetConstructor(Type.EmptyTypes).Invoke(null) as System.Exception;
            var mockDirectory = Mock.Of<Directory>();
            ErrorLogHelper.Instance._crashesDirectory = mockDirectory;
            Mock.Get(mockDirectory).Setup(d => d.EnumerateFiles(It.IsAny<string>())).Throws(exception);
            ErrorLogHelper.RemoveAllStoredErrorLogFiles();

            // No exception should be thrown.
        }
    }
}