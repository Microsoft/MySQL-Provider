﻿//-----------------------------------------------------------------------
// <copyright file="ApacheHTTPServerHealth.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <author>v-litin</author>
// <description>ApacheHTTPServerHealth</description>
//-----------------------------------------------------------------------

namespace Scx.Test.MySQL.SDK.MySQLSDKTests
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using Infra.Frmwrk;
    using Microsoft.EnterpriseManagement.Configuration;
    using Microsoft.EnterpriseManagement.Monitoring;
    using Scx.Test.Common;
    using Scx.Test.MySQL.SDK.MySQLSDKHelper;
    class ApacheHTTPServerHealth : PerformanceHealthBase, ISetup, IRun, IVerify, ICleanup
    {
         /// <summary>
        /// Initializes a new instance of the PerformanceHealth class
        /// </summary>
        public ApacheHTTPServerHealth()
        {
        }

        #region Test Framework Methods

        /// <summary>
        /// Framework setup method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void ISetup.Setup(IContext ctx)
        {
            ctx.Trc("Apache.SDKTests.PerformanceHealth.Setup");

            try
            {
                this.Info = new OMInfo(
                    ctx.ParentContext.Records.GetValue("omserver"),
                    ctx.ParentContext.Records.GetValue("omusername"),
                    ctx.ParentContext.Records.GetValue("omdomain"),
                    ctx.ParentContext.Records.GetValue("ompassword"),
                    ctx.ParentContext.Records.GetValue("defaultresourcepool"));

                this.ClientInfo = new ClientInfo(
                    ctx.ParentContext.Records.GetValue("hostname"),
                    ctx.ParentContext.Records.GetValue("ipaddress"),
                    ctx.ParentContext.Records.GetValue("targetcomputerclass"),
                    ctx.ParentContext.Records.GetValue("managementpack"),
                    ctx.ParentContext.Records.GetValue("architecture"),
                    ctx.ParentContext.Records.GetValue("nonsuperuser"),
                    ctx.ParentContext.Records.GetValue("nonsuperuserpwd"),
                    ctx.ParentContext.Records.GetValue("superuser"),
                    ctx.ParentContext.Records.GetValue("superuserpwd"),
                    ctx.ParentContext.Records.GetValue("packagename"),
                    ctx.ParentContext.Records.GetValue("cleanupcommand"),
                    ctx.ParentContext.Records.GetValue("platformtag"));

                ctx.Trc("OMInfo: " + Environment.NewLine + this.Info.ToString());

                ctx.Trc("ClientInfo: " + Environment.NewLine + this.ClientInfo.ToString());
                this.MonitorHelper = new MonitorHelper(this.Info);

                this.AlertHelper = new AlertsHelper(this.Info);

                this.OverrideHelper = new OverrideHelper(ctx.Trc, this.Info, ctx.ParentContext.Records.GetValue("testingoverride"));

                this.ComputerObject = this.MonitorHelper.GetComputerObject(this.ClientInfo.HostName);

                this.DeleteMonitorOverride(ctx);

                this.ApplyDefaultMonitorOverride(ctx, 5);

                this.CloseMatchingAlerts(ctx);

                this.VerifyMonitor(ctx, HealthState.Success);

                this.VerifyAlert(ctx, false);

            }
            catch (Exception ex)
            {
                Abort(ctx, ex.ToString());
            }

            ctx.Trc("Apache.SDKTests.PerformanceHealth.Setup complete");
        }

        /// <summary>
        /// Framework Run method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void IRun.Run(IContext ctx)
        {
            string entity = ctx.Records.GetValue("entityname");
            string actionCmd = ctx.Records.GetValue("ActionCmd");
            string monitorName = ctx.Records.GetValue("monitorname");
            string diagnosticName = ctx.Records.GetValue("diagnostics");
            string recoveryCmd = ctx.Records.GetValue("recoverycmd");
            string targetOSClassName = ctx.ParentContext.Records.GetValue("targetosclass");

            if (SkipThisTest(ctx))
            {
                return;
            }

            ctx.Trc("Apache.SDKTests.PerformanceHealth.Run with entity " + entity);

            try
            {
                ApplyMonitorOverride(ctx, 5);

                ctx.Alw("Running command: " + actionCmd);
                RunCmd(actionCmd);

                string expectedMonitorState = ctx.Records.GetValue("ExpectedState");
                HealthState requiredState = this.GetRequiredState(expectedMonitorState);

                //// Waiting for 5 minutes directly to make sure that OM can get the latest monitor state
                //// because the latest monitor state might be the same as the initial state
                if (ctx.Records.GetValue("ExpectedState").Equals("success", StringComparison.CurrentCultureIgnoreCase))
                {
                    this.Wait(ctx, 300);

                    this.VerifyMonitor(ctx, requiredState);

                    this.VerifyAlert(ctx, false);
                }
                else
                {
                    this.VerifyMonitor(ctx, requiredState);

                    this.VerifyAlert(ctx, true);
                }

                // Run the recovery command
                if (!(string.IsNullOrEmpty(recoveryCmd)))
                {
                    ctx.Trc("Running recovery command: " + recoveryCmd);
                    RunCmd(recoveryCmd);
                    this.VerifyAlert(ctx, false);
                }
            }
            catch (Exception ex)
            {
                Fail(ctx, ex.ToString());
            }

            ctx.Trc("Apache.SDKTests.PerformanceHealth.Run complete");
        }

        /// <summary>
        /// Framework verify method
        /// </summary>
        /// <param name="ctx">current context</param>
        void IVerify.Verify(IContext ctx)
        {
            if (SkipThisTest(ctx))
            {
                return;
            }

            ctx.Trc("Apache.SDKTests.PerformanceHealth.Verify complete");
        }

        /// <summary>
        /// Framework cleanup method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void ICleanup.Cleanup(IContext ctx)
        {
            if (SkipThisTest(ctx))
            {
                return;
            }

            this.DeleteMonitorOverride(ctx);

            this.ApplyDefaultMonitorOverride(ctx, 5);

            this.VerifyMonitor(ctx, HealthState.Success);

            this.VerifyAlert(ctx, false);
           
            ctx.Trc("Apache.SDKTests.PerformanceHealth.Cleanup finished");
        }

        #endregion Test Framework Methods

        #region Private Methods

        /// <summary>
        /// Apply the monitor override in the varmap such that the monitor under test can easily be put in an error condition
        /// </summary>
        ///// <param name="ctx">Current context</param>
        ///// <param name="interval">Value to override monitor's Interval parameter to.  
        ///// This controls number of seconds the health service waits between polls of the monitor state.</param>
        private void ApplyMonitorOverride(IContext ctx, int interval)
        {
            string monitorName = ctx.Records.GetValue("monitorname");
            string monitorContext = ctx.Records.GetValue("monitorcontext");
            string monitorTarget = ctx.Records.GetValue("monitortarget");
            string monitorThreshold = ctx.Records.GetValue("monitorthreshold");
            string thresholdName = "Threshold";

            if (!string.IsNullOrEmpty(monitorThreshold))
            {
                this.OverrideHelper.SetClientMonitorParameter(this.ComputerObject, monitorName, monitorContext, monitorTarget, thresholdName, monitorThreshold);

                this.OverrideHelper.SetClientMonitorInterval(this.ComputerObject, monitorName, monitorContext, monitorTarget, interval);
                
            }
        }

        ///// <summary>
        ///// Apply a monitor override to put the monitor threshold back to in the default value, as defined in the varmap
        ///// </summary>
        ///// <param name="ctx">Current context</param>
        ///// <param name="interval">Value to override monitor's Interval parameter to.  
        ///// This controls number of seconds the health service waits between polls of the monitor state.</param>
        private void ApplyDefaultMonitorOverride(IContext ctx, int interval)
        {
            string monitorName = ctx.Records.GetValue("monitorname");
            string monitorContext = ctx.Records.GetValue("monitorcontext");
            string monitorTarget = ctx.Records.GetValue("monitortarget");
            string monitorThreshold = ctx.Records.GetValue("defaultmonitorthreshold");
            string thresholdName = "Threshold";

            if (!string.IsNullOrEmpty(monitorThreshold))
            {
                this.OverrideHelper.SetClientMonitorParameter(this.ComputerObject, monitorName, monitorContext, monitorTarget, thresholdName, monitorThreshold);

                this.OverrideHelper.SetClientMonitorInterval(this.ComputerObject, monitorName, monitorContext, monitorTarget, interval);
                
            }
        }

        ///// <summary>
        ///// Apply a monitor override to put the monitor threshold back to in the default value, as defined in the varmap
        ///// </summary>
        ///// <param name="ctx">Current context</param>
        private void DeleteMonitorOverride(IContext ctx)
        {
            string monitorName = ctx.Records.GetValue("monitorname");
            string monitorContext = ctx.Records.GetValue("monitorcontext");
            string monitorTarget = ctx.Records.GetValue("monitortarget");
            string monitorThresholdStr = ctx.Records.GetValue("defaultmonitorthreshold");

            this.OverrideHelper.DeleteClientMonitorThreshold(
                this.ComputerObject,
                monitorName,
                monitorContext,
                monitorTarget);

            this.OverrideHelper.DeleteClientMonitorInterval(
                this.ComputerObject,
                monitorName,
                monitorContext,
                monitorTarget);
        }


        private RunPosixCmd RunCmd(string cmd, string arguments = "")
        {
            // Begin cmd
            RunPosixCmd execCmd = new RunPosixCmd(this.ClientInfo.HostName, this.ClientInfo.SuperUser, this.ClientInfo.SuperUserPassword);

            // Execute command
            execCmd.FileName = cmd;
            execCmd.Arguments = arguments;
            execCmd.RunCmd();
            return execCmd;
        }

        #endregion
    }
}
