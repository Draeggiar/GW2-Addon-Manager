﻿using System.Dynamic;
using System.Threading.Tasks;
using GW2_Addon_Manager;
using GW2_Addon_Manager.App.Configuration;
using GW2_Addon_Manager.App.Configuration.Model;
using GW2_Addon_Manager.Dependencies.FileSystem;
using Moq;
using NUnit.Framework;

namespace ApplicationTests.Backend.Configuration
{
    [TestFixture]
    [Parallelizable]
    public class ConfigurationTests
    {
        private Mock<IConfigurationManager> _configManagerMock;
        private Mock<IUpdateHelper> _updateHelperMock;
        private Mock<IFileSystemManager> _fileSystemManagerMock;

        [SetUp]
        public void Init()
        {
            _configManagerMock = new Mock<IConfigurationManager>();
            _configManagerMock.SetupGet(x => x.UserConfig).Returns(new UserConfig());

            _updateHelperMock = new Mock<IUpdateHelper>();
            _fileSystemManagerMock = new Mock<IFileSystemManager>();
        }

        [TestCase("v.1.0", "v.1.1", ExpectedResult = true)]
        [TestCase("v.1.0", "v.1.0", ExpectedResult = false)]
        public async Task<bool> ShouldCheck_IfNewVersionIsAvailable(string currentVersion, string latestVersion)
        {
            _configManagerMock.SetupGet(x => x.ApplicationVersion).Returns(currentVersion);
            _updateHelperMock.Setup(x => x.GitReleaseInfoAsync(It.IsAny<string>())).ReturnsAsync(() =>
            {
                dynamic result = new ExpandoObject();
                result.tag_name = latestVersion;
                return result;
            });
            
            var configuration =
                new GW2_Addon_Manager.Configuration(_configManagerMock.Object, _updateHelperMock.Object, _fileSystemManagerMock.Object);

            var checkResult = await configuration.CheckIfNewVersionIsAvailableAsync();

            Assert.That(checkResult.latestVersion, Is.EqualTo(latestVersion));
            return checkResult.isUpdateAvailable;
        }

        [TestCase("bin64")]
        [TestCase("bin")]
        public void ShouldDetermine_SystemType_AndSaveItInConfiguration(string systemType)
        {
            var configurationManager = _configManagerMock.Object;
            _fileSystemManagerMock.Setup(x => x.DirectoryExists($"{configurationManager.UserConfig.GamePath}")).Returns(true);
            _fileSystemManagerMock.Setup(x => x.DirectoryExists($"{configurationManager.UserConfig.GamePath}\\{systemType}")).Returns(true);

            var configuration = new GW2_Addon_Manager.Configuration(configurationManager, _updateHelperMock.Object,
                _fileSystemManagerMock.Object);

            configuration.DetermineSystemType();

            Assert.That(configurationManager.UserConfig.BinFolder, Is.EqualTo(systemType));
            Assert.That(configurationManager.UserConfig.ExeName, Is.EqualTo(systemType.EndsWith("64") ? "Gw2-64.exe" : "Gw2.exe"));
        }

        [Test]
        public void ShouldRemove_AllAddonsData_FromConfiguration()
        {
            _fileSystemManagerMock.Setup(x => x.DirectoryExists(It.IsAny<string>())).Returns(false);

            var configurationManager = _configManagerMock.Object;
            var configuration = new GW2_Addon_Manager.Configuration(configurationManager, _updateHelperMock.Object,
                _fileSystemManagerMock.Object);

            configuration.DeleteAllAddons();

            Assert.That(configurationManager.UserConfig.AddonsList, Is.Empty);
            Assert.That(configurationManager.UserConfig.LoaderVersion, Is.Null);
        }

        [Test]
        public async Task ShouldTakeLatestVersion_IfNewestVersionIsNull()
        {
            _configManagerMock.SetupGet(x => x.ApplicationVersion).Returns("1.0");
            _updateHelperMock.Setup(x => x.GitReleaseInfoAsync(It.IsAny<string>())).ReturnsAsync(default);

            var configuration =
                new GW2_Addon_Manager.Configuration(_configManagerMock.Object, _updateHelperMock.Object, _fileSystemManagerMock.Object);

            var checkResult = await configuration.CheckIfNewVersionIsAvailableAsync();

            Assert.That(checkResult.isUpdateAvailable, Is.False);
            Assert.That(checkResult.latestVersion, Is.EqualTo("1.0"));
        }
    }
}