using System;
using System.Activities;
using System.Activities.Statements;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using Microsoft.TeamFoundation.Build.Activities.Core;
using Microsoft.TeamFoundation.Build.Activities.Extensions;
using Microsoft.TeamFoundation.Build.Workflow;
using Microsoft.TeamFoundation.Build.Workflow.Activities;
using Microsoft.TeamFoundation.Build.Workflow.Tracking;
using Microsoft.TeamFoundation.Build.Client;
using Microsoft.TeamFoundation.Build.Activities.NuGet;

namespace vs2017xaml
{
    [BuildCategory]
    [BuildActivity(HostEnvironmentOption.All)]
    [Designer(typeof(Microsoft.TeamFoundation.Build.Workflow.Design.TeamBuildBaseActivityDesigner))]
    [ActivityTracking(ActivityTrackingOption.ActivityOnly)]
    public sealed class NewMSBuild : BaseActivity
    {
        public NewMSBuild()
        {
            this.ToolPlatform = Microsoft.TeamFoundation.Build.Workflow.Activities.ToolPlatform.Auto.ToString(); // Default to Auto
            this.Verbosity = BuildVerbosity.Normal.ToString(); // Default to Normal
            this.OutputLocation = NewBuildOutputLocation.SingleFolder.ToString(); // Default to SingleFolder
            this.MSBuildMultiProc = true;
            this.RestoreNuGetPackages = true;
        }

        #region Public Properties

        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<Boolean> CleanBuild
        {
            get
            {
                return m_cleanBuild;
            }
            set
            {
                m_cleanBuild = value;
                m_cleanBuildSet = true;
            }
        }


        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<Boolean> RestoreNuGetPackages
        {
            get
            {
                return m_restoreNuGetPackages;
            }
            set
            {
                m_restoreNuGetPackages = value;
                m_restoreNuGetPackagesSet = true;
            }
        }

        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<Boolean> MSBuildMultiProc
        {
            get
            {
                return m_msBuildMultiProc;
            }
            set
            {
                m_msBuildMultiProc = value;
                m_msBuildMultiProcSet = true;
            }
        }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String[]> ConfigurationsToBuild { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> RunCodeAnalysis { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String[]> ProjectsToBuild { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> OutDir { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> OutputLocation { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<IEnumerable<String>> Targets { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> ToolPlatform { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> ToolVersion { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> Verbosity { get; set; }

        [Browsable(true)]
        [DefaultValue(null)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        public InArgument<String> CommandLineArguments { get; set; }

        #endregion

        #region XAML Serialization Controls

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Boolean ShouldSerializeMSBuildMultiProc()
        {
            return m_msBuildMultiProcSet;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Boolean ShouldSerializeCleanBuild()
        {
            return m_cleanBuildSet;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public Boolean ShouldSerializeRestoreNuGetPackages()
        {
            return m_restoreNuGetPackagesSet;
        }

        #endregion

        public override Activity CreateBody()
        {
            Variable<Int32> maxProcesses = new Variable<Int32>();
            Variable<String[]> targets = new Variable<String[]>();
            Variable<ToolPlatform> toolPlatform = new Variable<ToolPlatform>();
            Variable<BuildVerbosity> buildVerbosity = new Variable<BuildVerbosity>();
            Variable<NewBuildOutputLocation> outputLocation = new Variable<NewBuildOutputLocation>();
            Variable<IBuildDetail> buildDetail = new Variable<IBuildDetail>();
            Variable<String> logFileDropLocation = new Variable<String>();
            Variable<String> outputDirectory = new Variable<String>();
            Variable<CodeAnalysisOption> runCodeAnalysis = new Variable<CodeAnalysisOption>();
            Variable<IEnumerable<PlatformConfiguration>> platformConfigurationList = new Variable<IEnumerable<PlatformConfiguration>>();
            Variable<ICollection<IQueuedBuild>> failedRequests = new Variable<ICollection<IQueuedBuild>>();
            Variable<Boolean> skipCleanBuild = new Variable<Boolean>();

            return new Sequence
            {
                Activities =
                {
                    new Sequence
                    {
                        Variables =
                        {
                            buildDetail,
                        },
                        Activities =
                        {
                            new TryCatch
                            {
                                Try = new Sequence
                                {
                                    Variables =
                                    {
                                        targets,
                                        maxProcesses,
                                        toolPlatform,
                                        buildVerbosity,
                                        outputLocation,
                                        runCodeAnalysis,
                                        outputDirectory,
                                        logFileDropLocation,
                                        platformConfigurationList,
                                        skipCleanBuild,
                                    },
                                    Activities =
                                    {
                                        // Before we start calling MSBuild, make sure all required NuGet packages are installed
                                        new If
                                        {
                                            Condition = new InArgument<Boolean>(env => this.RestoreNuGetPackages.Get(env)),
                                            Then = new NuGetRestore
                                            {
                                                Solutions = new InArgument<String[]>(env => this.ProjectsToBuild.Get(env)),
                                            },
                                        },
                                        // Default the log file drop location
                                        new Assign<String>
                                        {
                                            Value = new GetDefaultLogLocation(),
                                            To = logFileDropLocation
                                        },
                                        // Default the output directory if not provided
                                        /*
                                        new GetArgumentOrDefault
                                        {
                                            ArgumentValue = new InArgument<String>(env => this.OutDir.Get(env)),
                                            EnvironmentVariableName = WellKnownEnvironmentVariables.BinariesDirectory,
                                            Result = outputDirectory
                                        },
                                        */
                                        // Either use the passed in string value for RunCodeAnalysis or default it to AsConfigured
                                        new If
                                        {
                                            Condition = new InArgument<Boolean>(env => !String.IsNullOrEmpty(this.RunCodeAnalysis.Get(env))),
                                            Then = new Assign<CodeAnalysisOption>
                                            {
                                                Value = new InArgument<CodeAnalysisOption>(env => (CodeAnalysisOption)Enum.Parse(typeof(CodeAnalysisOption), this.RunCodeAnalysis.Get(env))),
                                                To = runCodeAnalysis
                                            },
                                            Else = new Assign<CodeAnalysisOption>
                                            {
                                                Value = new InArgument<CodeAnalysisOption>(CodeAnalysisOption.AsConfigured),
                                                To = runCodeAnalysis
                                            }
                                        },
                                        // If no ConfigurationsToBuild were passed, default to Default
                                        new If
                                        {
                                            Condition = new InArgument<Boolean>(env => (this.ConfigurationsToBuild.Get(env) == null || this.ConfigurationsToBuild.Get(env).Length == 0)),
                                            Then = new Assign<IEnumerable<PlatformConfiguration>>
                                            {
                                                Value = new InArgument<IEnumerable<PlatformConfiguration>>(env => (PlatformConfigurationList.Default)),
                                                To = platformConfigurationList
                                            },
                                            Else = new Assign<IEnumerable<PlatformConfiguration>>
                                            {
                                                Value = new InArgument<IEnumerable<PlatformConfiguration>>(env => new PlatformConfigurationList(this.ConfigurationsToBuild.Get(env))),
                                                To = platformConfigurationList
                                            }
                                        },
                                        new Assign<Int32>
                                        {
                                            Value = new InArgument<Int32>(env => this.MSBuildMultiProc.Get(env) ? 0 : 1),
                                            To = maxProcesses
                                        },
                                        new Assign<ToolPlatform>
                                        {
                                            Value = new InArgument<ToolPlatform>(env => (ToolPlatform)Enum.Parse(typeof(ToolPlatform),this.ToolPlatform.Get(env))),
                                            To = toolPlatform
                                        },
                                        new Assign<BuildVerbosity>
                                        {
                                            Value = new InArgument<BuildVerbosity>(env => (BuildVerbosity)Enum.Parse(typeof(BuildVerbosity), this.Verbosity.Get(env))),
                                            To = buildVerbosity
                                        },
                                        new Assign<NewBuildOutputLocation>
                                        {
                                            Value = new InArgument<NewBuildOutputLocation>(env => (NewBuildOutputLocation)Enum.Parse(typeof(NewBuildOutputLocation), this.OutputLocation.Get(env))),
                                            To = outputLocation
                                        },
                                        // Run Clean targets if CleanBuild param is true and workspace or repository hasn't already been cleaned
                                        new GetEnvironmentVariable<Boolean>
                                        {
                                            Name = WellKnownEnvironmentVariables.SkipCleanBuild,
                                            Result = skipCleanBuild,
                                        },
                                        new If
                                        {
                                            Condition = new InArgument<Boolean>(env => this.CleanBuild.Get(env) && !skipCleanBuild.Get(env)),
                                            Then = new Sequence
                                            {
                                                Activities =
                                                {
                                                    // Make sure we delete the bin folder, otherwise garbage collects from assembly renames, etc.
                                                    new DeleteDirectory
                                                    {
                                                        Directory = outputDirectory,
                                                        Recursive = true
                                                    },
                                                    new RunMSBuildInternal
                                                    {
                                                        Targets = new InArgument<IEnumerable<String>>(env => new String[] { "Clean" }),
                                                        PlatformConfigurations = platformConfigurationList,
                                                        ProjectsToBuild = new InArgument<IEnumerable<String>>(env => this.ProjectsToBuild.Get(env)),
                                                        CommandLineArguments = new InArgument<String>(env => this.CommandLineArguments.Get(env)),
                                                        LogFileDropLocation = String.Empty, // No logs needed here
                                                        MaxProcesses = maxProcesses,
                                                        OutputDirectory = outputDirectory,
                                                        OutputLocation = outputLocation,
                                                        RunCodeAnalysis = CodeAnalysisOption.Never, // don't run code analysis during this step
                                                        ToolPlatform = toolPlatform,
                                                        ToolVersion = new InArgument<String>(env => this.ToolVersion.Get(env)),
                                                        Verbosity = buildVerbosity,
                                                    },
                                                },
                                            },
                                        },
                                        // Build Targets passed in (or default)
                                        new RunMSBuildInternal
                                        {
                                            Targets = new InArgument<IEnumerable<String>>(env => this.Targets.Get(env)),
                                            PlatformConfigurations = platformConfigurationList,
                                            ProjectsToBuild = new InArgument<IEnumerable<String>>(env => this.ProjectsToBuild.Get(env)),
                                            CommandLineArguments = new InArgument<String>(env => this.CommandLineArguments.Get(env)),
                                            LogFileDropLocation = logFileDropLocation,
                                            MaxProcesses = maxProcesses,
                                            OutputDirectory = outputDirectory,
                                            OutputLocation = outputLocation,
                                            RunCodeAnalysis = runCodeAnalysis,
                                            ToolPlatform = toolPlatform,
                                            ToolVersion = new InArgument<String>(env => this.ToolVersion.Get(env)),
                                            Verbosity = buildVerbosity,
                                        },
                                    }
                                },
                                Catches =
                                {
                                    new Catch<Exception>
                                    {
                                        Action = new ActivityAction<Exception>
                                        {
                                            Handler = new Sequence
                                            {
                                                Variables =
                                                {
                                                    failedRequests,
                                                },
                                                Activities =
                                                {
                                                    new SetBuildProperties
                                                    {
                                                        CompilationStatus = Microsoft.TeamFoundation.Build.Client.BuildPhaseStatus.Failed,
                                                        PropertiesToSet = Microsoft.TeamFoundation.Build.Client.BuildUpdate.CompilationStatus,
                                                    },
                                                    new GetApprovedRequests
                                                    {
                                                        Result = failedRequests,
                                                    },
                                                    new RetryRequests
                                                    {
                                                        Behavior = new InArgument<RetryBehavior>(RetryBehavior.DoNotBatch),
                                                        Requests = failedRequests,
                                                    },
                                                    new Rethrow
                                                    {
                                                        // Rethrow the exception so the build will stop
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            },
                            new GetBuildDetail
                            {
                                Result = new OutArgument<IBuildDetail>(buildDetail),
                            },
                            new If
                            {
                                Condition = new InArgument<Boolean>(env => (buildDetail.Get(env).CompilationStatus == BuildPhaseStatus.Unknown)),
                                Then = new SetBuildProperties
                                {
                                    PropertiesToSet = Microsoft.TeamFoundation.Build.Client.BuildUpdate.CompilationStatus,
                                    CompilationStatus = Microsoft.TeamFoundation.Build.Client.BuildPhaseStatus.Succeeded
                                }
                            }
                        },
                    }
                }
            };
        }

        #region Private Members
        private Boolean m_cleanBuildSet;
        private InArgument<Boolean> m_cleanBuild;
        private Boolean m_msBuildMultiProcSet;
        private InArgument<Boolean> m_msBuildMultiProc;
        private Boolean m_restoreNuGetPackagesSet;
        private InArgument<Boolean> m_restoreNuGetPackages;
        #endregion

        private class RunMSBuildInternal : Activity
        {
            public RunMSBuildInternal()
            {
                Implementation = () => CreateBody();
            }

            public InArgument<IEnumerable<PlatformConfiguration>> PlatformConfigurations { get; set; }
            public InArgument<IEnumerable<String>> ProjectsToBuild { get; set; }
            public InArgument<Int32> MaxProcesses { get; set; }
            public InArgument<CodeAnalysisOption> RunCodeAnalysis { get; set; }
            public InArgument<String> OutputDirectory { get; set; }
            public InArgument<IEnumerable<String>> Targets { get; set; }
            public InArgument<ToolPlatform> ToolPlatform { get; set; }
            public InArgument<BuildVerbosity> Verbosity { get; set; }
            public InArgument<String> CommandLineArguments { get; set; }
            public InArgument<String> LogFileDropLocation { get; set; }
            public InArgument<NewBuildOutputLocation> OutputLocation { get; set; }
            public InArgument<String> ToolVersion { get; set; }

            private Activity CreateBody()
            {
                DelegateInArgument<PlatformConfiguration> platformConfiguration = new DelegateInArgument<PlatformConfiguration>();
                DelegateInArgument<String> projectToBuild = new DelegateInArgument<String>();
                Variable<String> projectLocation = new Variable<String>();
                Variable<String> outputDirectory = new Variable<String>();
                Variable<String> outputDirectoryForProject = new Variable<String>();

                return new ForEach<PlatformConfiguration>
                {
                    Values = new InArgument<IEnumerable<PlatformConfiguration>>(env => this.PlatformConfigurations.Get(env)),
                    Body = new ActivityAction<PlatformConfiguration>
                    {
                        Argument = platformConfiguration,
                        Handler = new Sequence
                        {
                            Variables =
                            {
                                outputDirectory,
                            },
                            Activities =
                            {
                                new GetPlatformConfigurationOutputDirectory
                                {
                                    PlatformConfiguration = platformConfiguration,
                                    PlatformConfigurationCount = new InArgument<Int32>(env => this.PlatformConfigurations.Get(env).Count()),
                                    OutputDirectory = new InArgument<String>(env => this.OutputDirectory.Get(env)),
                                    Result = outputDirectory,
                                },
                                // Start ForEach Project in ProjectsToBuild
                                new ForEach<String>
                                {
                                    Values = new InArgument<IEnumerable<String>>(env => this.ProjectsToBuild.Get(env)),
                                    Body = new ActivityAction<String>
                                    {
                                        Argument = projectToBuild,
                                        Handler = new Sequence
                                        {
                                            Variables =
                                            {
                                                projectLocation,
                                                outputDirectoryForProject,
                                            },
                                            Activities =
                                            {
                                                // Get the project location from the relative path
                                                new GetLocalPath
                                                {
                                                    IncomingPath = new InArgument<String>(env => projectToBuild.Get(env)),
                                                    Result = projectLocation,
                                                },
                                                // Reset the outputDirectory based on the output location
                                                new If
                                                {
                                                    Condition = new InArgument<Boolean>(env => this.OutputLocation.Get(env) == NewBuildOutputLocation.SingleFolder),
                                                    Then = new Assign<String>
                                                    {
                                                        Value = new InArgument<String>(env => outputDirectory.Get(env)),
                                                        To = outputDirectoryForProject,
                                                    }
                                                },
                                                new If
                                                {
                                                    Condition = new InArgument<Boolean>(env => this.OutputLocation.Get(env) == NewBuildOutputLocation.PerProject),
                                                    Then = new Assign<String>
                                                    {
                                                        Value = new InArgument<String>(env => Path.Combine(outputDirectory.Get(env), Path.GetFileNameWithoutExtension(projectLocation.Get(env)))),
                                                        To = outputDirectoryForProject,
                                                    }
                                                },
                                                new If
                                                {
                                                    Condition = new InArgument<Boolean>(env => this.OutputLocation.Get(env) == NewBuildOutputLocation.AsConfigured),
                                                    Then = new Assign<String>
                                                    {
                                                        Value = String.Empty,
                                                        To = outputDirectoryForProject,
                                                    }
                                                },
                                                new MSBuild
                                                {
                                                    CommandLineArguments = new InArgument<String>(env => this.CommandLineArguments.Get(env)),
                                                    Configuration = new InArgument<String>(env => platformConfiguration.Get(env).Configuration),
                                                    GenerateVSPropsFile = new InArgument<Boolean>(true),
                                                    LogFileDropLocation = new InArgument<String>(env => this.LogFileDropLocation.Get(env)),
                                                    MaxProcesses = new InArgument<Int32>(env => this.MaxProcesses.Get(env)),
                                                    OutDir = outputDirectoryForProject,
                                                    Platform = new InArgument<String>(env => platformConfiguration.Get(env).Platform),
                                                    Project = projectLocation,
                                                    RunCodeAnalysis = new InArgument<CodeAnalysisOption>(env => this.RunCodeAnalysis.Get(env)),
                                                    Targets = new InArgument<IEnumerable<String>>(env => this.Targets.Get(env)),
                                                    TargetsNotLogged = new InArgument<IEnumerable<String>>(env => new List<String> { "GetNativeManifest", "GetCopyToOutputDirectoryItems", "GetTargetPath" }),
                                                    ToolPlatform = new InArgument<ToolPlatform>(env => this.ToolPlatform.Get(env)),
                                                    Verbosity = new InArgument<BuildVerbosity>(env => this.Verbosity.Get(env)),
                                                    ToolVersion = new InArgument<String>(env => String.IsNullOrEmpty(this.ToolVersion.Get(env)) ? "latest" : this.ToolVersion.Get(env)),
                                                    ToolPath = "C:\\Program Files (x86)\\Microsoft Visual Studio\\2017\\Professional\\MSBuild\\15.0\\Bin"
                                                },
                                            }
                                        }
                                    }
                                } // End ForEach Project in ProjectsToBuild
                            }
                        }
                    }
                }; // End ForEach PlatformConfiguration in PlatformConfigurationList
            }
        }

        private class GetDefaultLogLocation : CodeActivity<String>
        {
            protected override string Execute(CodeActivityContext context)
            {
                IEnvironmentVariableExtension envVarExt = context.GetExtension<IEnvironmentVariableExtension>();
                String dropLocation = envVarExt.GetEnvironmentVariable<String>(context, WellKnownEnvironmentVariables.DropLocation);
                return BuildDropProvider.CombinePaths(dropLocation, "logs");
            }
        }
    }

    internal class GetPlatformConfigurationOutputDirectory : CodeActivity<String>
    {
        public InArgument<PlatformConfiguration> PlatformConfiguration { get; set; }
        public InArgument<Int32> PlatformConfigurationCount { get; set; }
        public InArgument<String> OutputDirectory { get; set; }

        protected override String Execute(CodeActivityContext context)
        {
            String outputDirectory = OutputDirectory.Get(context);
            PlatformConfiguration platformConfiguration = PlatformConfiguration.Get(context);
            Int32 platformConfigurationCount = PlatformConfigurationCount.Get(context);

            if (!platformConfiguration.IsEmpty && platformConfigurationCount > 1)
            {
                if (platformConfiguration.IsPlatformEmptyOrAnyCpu)
                {
                    outputDirectory = Path.Combine(outputDirectory, platformConfiguration.Configuration);
                }
                else
                {
                    outputDirectory = Path.Combine(outputDirectory, platformConfiguration.Platform, platformConfiguration.Configuration);
                }
            }

            return outputDirectory;
        }
    }

    public enum NewBuildOutputLocation
    {
        SingleFolder = 0,
        PerProject = 1,
        AsConfigured = 2,
    }
}
