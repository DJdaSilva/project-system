﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.ComponentModel.Composition;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Construction;
using Microsoft.VisualStudio.IO;

namespace Microsoft.VisualStudio.ProjectSystem
{
    [Export(typeof(IProjectXmlAccessor))]
    internal class MSBuildXmlAccessor : IProjectXmlAccessor
    {
        private readonly IProjectLockService _projectLockService;
        private readonly UnconfiguredProject _unconfiguredProject;
        private readonly IFileSystem _fileSystem;

        [ImportingConstructor]
        public MSBuildXmlAccessor(IProjectLockService projectLockService,
            UnconfiguredProject unconfiguredProject,
            IFileSystem fileSystem)
        {
            Requires.NotNull(projectLockService, nameof(projectLockService));
            Requires.NotNull(unconfiguredProject, nameof(unconfiguredProject));
            Requires.NotNull(fileSystem, nameof(fileSystem));

            _projectLockService = projectLockService;
            _unconfiguredProject = unconfiguredProject;
            _fileSystem = fileSystem;
        }

        public async Task<string> GetProjectXmlAsync()
        {
            using (var access = await _projectLockService.ReadLockAsync())
            {
                var projectXml = await access.GetProjectXmlAsync(_unconfiguredProject.FullPath).ConfigureAwait(true);
                return projectXml.RawXml;
            }
        }

        public async Task<string> GetEvaluatedPropertyValue(UnconfiguredProject unconfiguredProject, string propertyName)
        {
            var configuredProject = await unconfiguredProject.GetSuggestedConfiguredProjectAsync().ConfigureAwait(false);
            using (var access = await _projectLockService.ReadLockAsync())
            {
                var evaluatedProject = await access.GetProjectAsync(configuredProject).ConfigureAwait(false);
                return evaluatedProject.GetProperty(propertyName)?.EvaluatedValue;
            }
        }

        public async Task ExecuteInWriteLock(Action<ProjectRootElement> action)
        {
            using (var access = await _projectLockService.WriteLockAsync())
            {
                await access.CheckoutAsync(_unconfiguredProject.FullPath).ConfigureAwait(true);
                var msbuildProject = await access.GetProjectXmlAsync(_unconfiguredProject.FullPath).ConfigureAwait(false);
                action.Invoke(msbuildProject);
            }
        }

        public async Task<ICollection<(string evaluatedInclude, string metadataValue)>> GetItems(ConfiguredProject configuredProject, string itemType, string metadataName)
        {
            using (var access = await _projectLockService.ReadLockAsync())
            {
                var project = await access.GetProjectAsync(configuredProject).ConfigureAwait(true);
                return project.GetItems(itemType: itemType).Select(i => (i.EvaluatedInclude, i.GetMetadataValue(metadataName))).ToArray();
            }
        }
    }
}
