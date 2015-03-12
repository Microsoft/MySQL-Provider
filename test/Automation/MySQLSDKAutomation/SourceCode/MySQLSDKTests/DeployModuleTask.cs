﻿//-----------------------------------------------------------------------
// <copyright file="DeployModuleTask.cs" company="Microsoft">
//     Copyright (c) Microsoft Corporation.  All rights reserved.
// </copyright>
// <author>v-jeyin</author>
// <description></description>
// <history>12/10/2014 2:30:10 PM: Created</history>
//-----------------------------------------------------------------------

namespace Scx.Test.MySQL.SDK.MySQLSDKTests
{
    using System;
    using System.Collections.Generic;
    using Infra.Frmwrk;    
    using Microsoft.EnterpriseManagement.Monitoring;
    using Scx.Test.Common;
    using Scx.Test.MySQL.SDK.MySQLSDKHelper;
    using System.Text.RegularExpressions;

    /// <summary>
    /// Description for DeployModuleTask.
    /// Test Case 798925: [MySQL]<SDK><Task> Verify the MySQL Server Provide installation failed  by running task "Install/Upgrade MySQL Server Provider" with root account which password is incorrect.
    /// </summary>
    public class DeployModuleTask : ISetup, IRun, IVerify, ICleanup
    {
        #region Private Fields

        /// <summary>
        /// Imformation about the OM server.
        /// </summary>
        private OMInfo info;

        /// <summary>
        /// Information about the client machine
        /// </summary>
        private ClientInfo clientInfo;

        /// <summary>
        /// Helper class to check status of monitors.
        /// </summary>
        private MonitorHelper monitorHelper;

        /// <summary>
        /// A MonitoroingObject representing the client machine.
        /// </summary>
        private MonitoringObject mysqlInstance;

        /// <summary>
        /// Helper class to run task
        /// </summary>
        private TasksHelper tasksHelper;

        /// <summary>
        /// Discovery helper class
        /// </summary>
        private DiscoveryHelper discoveryHelper;

        /// <summary>
        /// MySQL agent helper class
        /// </summary>
        private MySQLAgentHelper mysqlAgentHelper;

        /// <summary>
        /// object to hosd consumer task result
        /// </summary>
        private IList<Microsoft.EnterpriseManagement.Runtime.TaskResult> taskResult;

        /// <summary>
        /// The Location of MySQL Cim module
        /// </summary>
        private string tempMySQLCIMModuleLocation= @"C:\Windows\Temp\MySQLCimProv";

        #endregion Private Fields

        #region Constructors

        /// <summary>
        /// Initializes a new instance of the DeployModuleTask class.
        /// </summary>
        public DeployModuleTask()
        {
        }

        #endregion Constructors

        #region Methods

        #region Test Framework Methods

        /// <summary>
        /// Framework setup method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void ISetup.Setup(IContext ctx)
        {
            ctx.Trc("MySQLSDKTests.DeployModuleTask.Setup");

            try
            {
                this.info = new OMInfo(
                    ctx.ParentContext.Records.GetValue("omserver"),
                    ctx.ParentContext.Records.GetValue("omusername"),
                    ctx.ParentContext.Records.GetValue("omdomain"),
                    ctx.ParentContext.Records.GetValue("ompassword"),
                    ctx.ParentContext.Records.GetValue("defaultresourcepool"));

                this.clientInfo = new ClientInfo(
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

                ctx.Trc("OMInfo: " + Environment.NewLine + this.info.ToString());

                ctx.Trc("ClientInfo: " + Environment.NewLine + this.clientInfo.ToString());

                this.tasksHelper = new TasksHelper(ctx.Trc, this.info);

                this.monitorHelper = new MonitorHelper(this.info);

                string monitorInstanceClass = ctx.Records.GetValue("InstanceClass");

                this.mysqlInstance = this.monitorHelper.GetMonitoringObject(monitorInstanceClass,this.clientInfo.HostName);
                // Configure the agent maintenance account according to the cases. using root account by default.
                if (ctx.Records.HasKey("UseAgentMaintenanceAccount") && ctx.Records.GetValue("UseAgentMaintenanceAccount").Equals("true"))
                {
                    this.discoveryHelper.UseAgentMaintenanceAccount = true;
                    this.ConfigureAgentMaintenceAccount(ctx);
                }

                // Configure the agent maintenance account according to the cases. using root account by default.
                string agentCredentialSettings = ctx.Records.GetValue("agentCredentialSettings");
                if (!string.IsNullOrEmpty(agentCredentialSettings))
                {
                    this.ConfigureSpecialAgentMaintenceAccount(ctx);
                }

                //Uninstall MySQL CIm Module
                this.mysqlAgentHelper = new MySQLAgentHelper(this.info, this.clientInfo) { Logger = ctx.Trc };
                string fullMySQLAgentPath = tempMySQLCIMModuleLocation;
                if (ctx.ParentContext.Records.HasKey("useTaskInstallMySQLAgent") &&
                    ctx.ParentContext.Records.GetValue("useTaskInstallMySQLAgent").ToLower() == "false")
                {
                    fullMySQLAgentPath = ctx.ParentContext.Records.GetValue("mysqlAgentPath");
                }

                string needRemoveDefaultAgent = ctx.Records.GetValue("RemoveDefaultAgent");
                if (needRemoveDefaultAgent.ToLower().Contains("true"))
                {
                    string tag = ctx.ParentContext.Records.GetValue("mysqlTag");
                    this.mysqlAgentHelper.UninstallMySQLAgentWihCommand(fullMySQLAgentPath, tag);
                }
             }
            catch (Exception ex)
            {
                this.Abort(ctx, ex.ToString());
            }

            ctx.Trc("MySQLSDKTests.DeployModuleTask.Setup complete");
        }

        /// <summary>
        /// Framework Run method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void IRun.Run(IContext ctx)
        {
            ctx.Trc("MySQLSDKTests.DeployModuleTask.Run");

            string entity = ctx.Records.GetValue("entityname");
            string consumerTask = ctx.Records.GetValue("taskname");
            string expectedResult = ctx.Records.GetValue("ExpectedTaskStatus");

            ctx.Trc("SDKTests.ConsumerTaskHealth.Run: ConsumerTaskHealth test: " + entity);
            
            try
            {
                if (expectedResult.Equals("Fail"))
                {
                    this.taskResult = this.tasksHelper.RunFailedTask(this.mysqlInstance, consumerTask);
                }
                else
                {
                this.taskResult = this.tasksHelper.RunConsumerTask(this.mysqlInstance, consumerTask);
            }
            }
            catch (Exception ex)
            {
                this.Abort(ctx, ex.ToString());
            }

            ctx.Trc("MySQLSDKTests.DeployModuleTask.Run complete");
        }

        /// <summary>
        /// Framework verify method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void IVerify.Verify(IContext ctx)
        {
            ctx.Trc("MySQLSDKTests.DeployModuleTask.Verify");
            try
            {
                Microsoft.EnterpriseManagement.Runtime.TaskStatus expectedTaskStatus = this.GetExpectedTaskStatus(ctx.Records.GetValue("expectedTaskStatus"));
                string expectedOutputKeyWord = ctx.Records.GetValue("expectedOutputKeyWord");

                if (this.taskResult != null)
                {   
                    ctx.Trc(String.Format("Pass: Consumer task execute fine:{0}", ctx.Records.GetValue("taskname")));
                }
                else
                {
                    this.Fail(ctx, "Consumer Task Executes Fail!");
                }

                if (this.taskResult[0].Status == expectedTaskStatus)
                {
                    ctx.Trc(string.Format("Pass: expectedTaskStatus: {0}, actualTaskStatus: {1}", expectedTaskStatus, this.taskResult[0].Status));
                }
                else
                {
                    this.Fail(ctx, string.Format("Fail: expectedTaskStatus: {0}, actualTaskStatus: {1}", expectedTaskStatus, this.taskResult[0].Status));
                }

                Regex re = new Regex(expectedOutputKeyWord);
                if (re.IsMatch(this.taskResult[0].Output))
                //if (this.taskResult[0].Output.Contains(expectedOutputKeyWord))
                {
                    ctx.Trc(string.Format("Pass: expectedOutputKeyWord: {0}, actualOutputKeyWord: {1}", expectedOutputKeyWord, this.taskResult[0].Output));
                }
                else
                {
                    this.Fail(ctx, string.Format("Fail: expectedOutputKeyWord: {0}, actualOutputKeyWord: {1}", expectedOutputKeyWord, this.taskResult[0].Output));
                }

                if (!(expectedTaskStatus == Microsoft.EnterpriseManagement.Runtime.TaskStatus.Failed))
                {
                bool mysqlAgentInstalled = this.mysqlAgentHelper.VerifyMySQLAgentInstalled();
                    if (!mysqlAgentInstalled & ctx.Records.GetValue("ExpectedTaskStatus").Contains("Success"))
                {
                    this.Fail(ctx, "Fail: didn't find CIM prov installed on client");
                }
            }
            }
            catch (Exception ex)
            {
                this.Fail(ctx, ex.Message);
            }

            ctx.Trc("MySQLSDKTests.DeployModuleTask.Verify.Complete!");
        }

        /// <summary>
        /// Framework cleanup method
        /// </summary>
        /// <param name="ctx">Current context</param>
        void ICleanup.Cleanup(IContext ctx)
        {
            ctx.Trc("MySQLSDKTests.DeployModuleTask.Cleanup");

            this.ConfigureSpecialAgentMaintenceAccount(ctx, true);

            bool useTaskInstallMySQLAgent = false;
            if (ctx.Records.HasKey("useTaskInstallMySQLAgent") &&
               ctx.Records.GetValue("useTaskInstallMySQLAgent") == "true")
            {
                useTaskInstallMySQLAgent = true;
            }
            
            string needRemoveDefaultAgent = ctx.Records.GetValue("RemoveDefaultAgent");
            if (needRemoveDefaultAgent.ToLower().Contains("true"))
            {
                bool mysqlAgentInstalled = this.mysqlAgentHelper.VerifyMySQLAgentInstalled();

                if (useTaskInstallMySQLAgent)
                {
                    if (!mysqlAgentInstalled)
                    {
                        this.mysqlAgentHelper.InstallMySQLAgentWihTask();
                    }
                }
                else
                {
                    string fullMySQLAgentPath = tempMySQLCIMModuleLocation;
                    string tag = ctx.ParentContext.Records.GetValue("mysqlTag");
                    if (mysqlAgentInstalled)
                    {
                        this.mysqlAgentHelper.UninstallMySQLAgentWihCommand(fullMySQLAgentPath, tag);
                    }

                    fullMySQLAgentPath = ctx.ParentContext.Records.GetValue("mysqlAgentPath");
                    this.mysqlAgentHelper.InstallMySQLAgentWihCommand(fullMySQLAgentPath, tag);
                }
            }

            ctx.Trc("MySQLSDKTests.DeployModuleTask.Cleanup finished");
        }

        #endregion Test Framework Methods

        #region Private Methods

        /// <summary>
        /// Get special task status object of Microsoft.EnterpriseManagement.Runtime.TaskStatus for the expect 
        /// </summary>
        /// <param name="expectedStatus">expected status</param>
        /// <returns>return Microsoft.EnterpriseManagement.Runtime.TaskStatus object</returns>
        private Microsoft.EnterpriseManagement.Runtime.TaskStatus GetExpectedTaskStatus(string expectedStatus)
        {
            switch (expectedStatus.ToLower())
            {
                case "success":
                    {
                        return Microsoft.EnterpriseManagement.Runtime.TaskStatus.Succeeded;
                    }

                case "fail":
                    {
                        return Microsoft.EnterpriseManagement.Runtime.TaskStatus.Failed;
                    }

                default:
                    {
                        return Microsoft.EnterpriseManagement.Runtime.TaskStatus.Scheduled;
                    }
            }
        }

        /// <summary>
        /// Fail the text case by printing out a log message and throwing an exception
        /// </summary>
        /// <param name="ctx">Current context</param>
        /// <param name="msg">Error message</param>
        private void Fail(IContext ctx, string msg)
        {
            ctx.Trc("SecurityHealth FAIL: " + msg);
            throw new VarFail(msg);
        }

        /// <summary>
        /// Abort the text case by printing out a log message and throwing an exception
        /// </summary>
        /// <param name="ctx">Current context</param>
        /// <param name="msg">Error message</param>
        private void Abort(IContext ctx, string msg)
        {
            ctx.Trc("SecurityHealth ABORT: " + msg);
            throw new VarAbort(msg);
        }

        /// <summary>
        /// Delete the exist agent maintenance account and create a new one 
        /// </summary>
        /// <param name="ctx">IContext ctx</param>
        private void ConfigureAgentMaintenceAccount(IContext ctx)
        {
            ManageAccountHelper manageAccountHelper = new ManageAccountHelper(this.info, ctx.Trc);

            string credentialSettings = ctx.Records.GetValue("agentcredentialSettings");

            string[] settings = credentialSettings.Split(new[] { ',' });
            if (settings.Length < 4)
            {
                throw new Exception("Agent credential setting is not correct");
            }

            ScxCredentialSettings.CredentialType credentialType = (ScxCredentialSettings.CredentialType)int.Parse(settings[0]);
            ScxCredentialSettings.AccountType accountType = (ScxCredentialSettings.AccountType)int.Parse(settings[1]);
            ScxCredentialSettings.SSHCredentialType sshCredentialType = (ScxCredentialSettings.SSHCredentialType)int.Parse(settings[2]);
            ScxCredentialSettings.ElevationType elevationType = (ScxCredentialSettings.ElevationType)int.Parse(settings[3]);

            string username;
            string password;
            if (accountType == ScxCredentialSettings.AccountType.Priviledged)
            {
                username = ctx.Records.GetValue("superuser");
                password = ctx.Records.GetValue("superuserpwd");
            }
            else
            {
                username = ctx.Records.GetValue("nonsuperuser");
                password = ctx.Records.GetValue("nonsuperuserpwd");
            }
        }

        /// <summary>
        /// Delete the exist agent maintenance account and create a new one 
        /// </summary>
        /// <param name="ctx">IContext ctx</param>
        private void ConfigureSpecialAgentMaintenceAccount(IContext ctx, bool useDefault = false)
        {

            ManageAccountHelper manageAccountHelper = new ManageAccountHelper(this.info, ctx.Trc);

            string credentialSettings = "";
            if (!useDefault)
            {
                credentialSettings = ctx.Records.GetValue("agentCredentialSettings");
            }
            else
            {
                credentialSettings = ctx.ParentContext.Records.GetValue("agentCredentialSettings");
            }

            string[] settings = credentialSettings.Split(new[] { ',' });
            if (settings.Length < 4)
            {
                throw new Exception("Agent credential setting is not correct");
            }

            ScxCredentialSettings.CredentialType credentialType = (ScxCredentialSettings.CredentialType)int.Parse(settings[0]);
            ScxCredentialSettings.AccountType accountType = (ScxCredentialSettings.AccountType)int.Parse(settings[1]);
            ScxCredentialSettings.SSHCredentialType sshCredentialType = (ScxCredentialSettings.SSHCredentialType)int.Parse(settings[2]);
            ScxCredentialSettings.ElevationType elevationType = (ScxCredentialSettings.ElevationType)int.Parse(settings[3]);

            string username;
            string password;
            if (accountType == ScxCredentialSettings.AccountType.Priviledged)
            {
                username = ctx.ParentContext.Records.GetValue("superuser");
                if (ctx.Records.GetValue("entityname").Contains("incorrect password of root account") && !useDefault)
                {
                    password = ctx.ParentContext.Records.GetValue("superuserpwd") + "11";
                }
                else
                {
                    password = ctx.ParentContext.Records.GetValue("superuserpwd");
                }

            }
            else
            {
                username = ctx.ParentContext.Records.GetValue("nonsuperuser");
                password = ctx.ParentContext.Records.GetValue("nonsuperuserpwd");
            }

            // Get Agent maintenance account.
            Credential credential = manageAccountHelper.GetAgentAccountCredential(credentialType, accountType, sshCredentialType, elevationType, username, password);

            // If Agent maintenance account not exists, create it.
            // if (credential == null)
            //{
            // If the account with the same display name exists but different configuration, delete it
            if (manageAccountHelper.IsUserAccountExists(ScxCredentialSettings.UnixAccountType.ScxAgentMaintenance))
            {
                manageAccountHelper.DeleteAccount(ScxCredentialSettings.UnixAccountType.ScxAgentMaintenance, ScxCredentialSettings.ProfileType.AgentMaintenanceAccount);
            }

            credential = manageAccountHelper.CreateAgentMaintenanceAccount(credentialType, accountType, sshCredentialType, elevationType, username, password);
            //}

        }

        #endregion Private Methods

        #endregion Methods
    }
}
