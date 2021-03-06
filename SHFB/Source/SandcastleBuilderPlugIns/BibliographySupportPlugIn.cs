﻿//===============================================================================================================
// System  : Sandcastle Help File Builder Plug-Ins
// File    : BibliographySupportPlugIn.cs
// Author  : Eric Woodruff  (Eric@EWoodruff.us)
// Updated : 12/22/2015
// Note    : Copyright 2008-2015, Eric Woodruff, All rights reserved
// Compiler: Microsoft Visual C#
//
// This file contains a plug-in that is used to add bibliography support to the topics
//
// This code is published under the Microsoft Public License (Ms-PL).  A copy of the license should be
// distributed with the code and can be found at the project website: https://GitHub.com/EWSoftware/SHFB.  This
// notice, the author's name, and all copyright notices must remain intact in all applications, documentation,
// and source files.
//
//    Date     Who  Comments
// ==============================================================================================================
// 11/07/2008  EFW  Created the code
// 12/17/2013  EFW  Updated to use MEF for the plug-ins
//===============================================================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Xml;
using System.Xml.XPath;

using Sandcastle.Core;

using SandcastleBuilder.Utils;
using SandcastleBuilder.Utils.BuildComponent;
using SandcastleBuilder.Utils.BuildEngine;

namespace SandcastleBuilder.PlugIns
{
    /// <summary>
    /// This plug-in class is used to add bibliography support to the topics
    /// </summary>
    [HelpFileBuilderPlugInExport("Bibliography Support", IsConfigurable = true,
      Version = AssemblyInfo.ProductVersion, Copyright = AssemblyInfo.Copyright,
      Description = "This plug-in is used to add bibliography support to the help file topics.")]
    public sealed class BibliographySupportPlugIn : SandcastleBuilder.Utils.BuildComponent.IPlugIn
    {
        #region Private data members
        //=====================================================================

        private List<ExecutionPoint> executionPoints;
        private BuildProcess builder;

        private string bibliographyFile;
        #endregion

        #region IPlugIn implementation
        //=====================================================================

        /// <summary>
        /// This read-only property returns a collection of execution points that define when the plug-in should
        /// be invoked during the build process.
        /// </summary>
        public IEnumerable<ExecutionPoint> ExecutionPoints
        {
            get
            {
                if(executionPoints == null)
                    executionPoints = new List<ExecutionPoint>
                    {
                        new ExecutionPoint(BuildStep.MergeCustomConfigs, ExecutionBehaviors.After)
                    };

                return executionPoints;
            }
        }

        /// <summary>
        /// This method is used by the Sandcastle Help File Builder to let the plug-in perform its own
        /// configuration.
        /// </summary>
        /// <param name="project">A reference to the active project</param>
        /// <param name="currentConfig">The current configuration XML fragment</param>
        /// <returns>A string containing the new configuration XML fragment</returns>
        /// <remarks>The configuration data will be stored in the help file builder project.</remarks>
        public string ConfigurePlugIn(SandcastleProject project, string currentConfig)
        {
            using(BibliographySupportConfigDlg dlg = new BibliographySupportConfigDlg(currentConfig))
            {
                if(dlg.ShowDialog() == DialogResult.OK)
                    currentConfig = dlg.Configuration;
            }

            return currentConfig;
        }

        /// <summary>
        /// This method is used to initialize the plug-in at the start of the build process
        /// </summary>
        /// <param name="buildProcess">A reference to the current build process</param>
        /// <param name="configuration">The configuration data that the plug-in should use to initialize itself</param>
        public void Initialize(BuildProcess buildProcess, XPathNavigator configuration)
        {
            XPathNavigator root, node;

            builder = buildProcess;

            var metadata = (HelpFileBuilderPlugInExportAttribute)this.GetType().GetCustomAttributes(
                typeof(HelpFileBuilderPlugInExportAttribute), false).First();

            builder.ReportProgress("{0} Version {1}\r\n{2}", metadata.Id, metadata.Version, metadata.Copyright);

            // If the file format is Open XML, this plug-in is not supported and will not run
            if((builder.CurrentProject.HelpFileFormat & HelpFileFormats.OpenXml) != 0)
            {
                builder.ReportWarning("BIP0005", "The bibliography plug-in is not supported in the Open XML " +
                    "file format and will not run.");
                executionPoints = new List<ExecutionPoint>();
                return;
            }

            root = configuration.SelectSingleNode("configuration");

            if(root.IsEmptyElement)
                throw new BuilderException("BIP0001", "The Bibliography support plug-in has not been " +
                    "configured yet");

            node = root.SelectSingleNode("bibliography");

            if(node != null)
                bibliographyFile = node.GetAttribute("path", String.Empty).Trim();

            if(String.IsNullOrEmpty(bibliographyFile))
                throw new BuilderException("BIP0002", "A path to the bibliography file is required");

            // If relative, the path is relative to the project folder
            bibliographyFile = FilePath.RelativeToAbsolutePath(builder.ProjectFolder,
                builder.SubstitutionTags.TransformText(bibliographyFile));

            if(!File.Exists(bibliographyFile))
                throw new BuilderException("BIP0003", "Unable to locate bibliography file at " + bibliographyFile);
        }

        /// <summary>
        /// This method is used to execute the plug-in during the build process
        /// </summary>
        /// <param name="context">The current execution context</param>
        public void Execute(ExecutionContext context)
        {
            XmlDocument configFile;
            XmlAttribute attr;
            XmlNode argument;
            string configFilename = builder.WorkingFolder + "sandcastle.config";

            if(!File.Exists(configFilename))
                return;

            builder.ReportProgress("\r\nAdding bibliography parameter to {0}...", configFilename);
            configFile = new XmlDocument();
            configFile.Load(configFilename);

            // Find the XSL Transform Components in the configuration file and add a new argument to them:
            // <argument key="bibliographyData" value="C:\Path\To\bibliography.xml" />
            XmlNodeList components = configFile.SelectNodes("//component[@id='XSL Transform Component']/transform");

            if(components.Count == 0)
                throw new BuilderException("BIP0004", "Unable to locate XSL Transform Component configuration in " +
                    configFilename);

            foreach(XmlNode transform in components)
            {
                argument = configFile.CreateElement("argument");

                attr = configFile.CreateAttribute("key");
                attr.Value = "bibliographyData";
                argument.Attributes.Append(attr);

                attr = configFile.CreateAttribute("value");
                attr.Value = bibliographyFile;
                argument.Attributes.Append(attr);

                transform.AppendChild(argument);
            }

            configFile.Save(configFilename);
        }
        #endregion

        #region IDisposable implementation
        //=====================================================================

        /// <summary>
        /// This implements the Dispose() interface to properly dispose of the plug-in object
        /// </summary>
        public void Dispose()
        {
            // Nothing to dispose of in this one
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
