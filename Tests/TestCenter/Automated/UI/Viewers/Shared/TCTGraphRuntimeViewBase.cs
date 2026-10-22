using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Xml.Linq;
using CxVisual.TestCenter.Models;
using CxVisual.TestCenter.Fixtures;
using CxVisual.Mechanism.Core;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using CxVisual.TestCenter.Core;

namespace CxVisual.TestCenter.UI
{
    public abstract class TCTGraphRuntimeViewBase : TCTTestViewBase
    {
        public string ArchivedEvidenceRoot { get; set; }
        public string EvidenceProblem { get; protected set; }
        public int VerifiedItems { get; private set; }
        public event Action<string, string> SourceSelected;

        public override void Clear()
        {
            base.Clear();
            EvidenceProblem = null; VerifiedItems = 0;
        }
        protected void Selected(TCTTestObservation observation, string id, string details)
        {
            SelectObservation(observation);
            if (SourceSelected != null) SourceSelected(id, details);
        }
        protected void Count() { VerifiedItems++; }
        protected abstract void Render(XDocument data, TCTTestObservation observation);
        protected void RenderEvidence(TCTTestCaseResult result)
        {
            foreach (TCTTestObservation observation in result.Observations.Where(o =>
                string.Equals(o.Kind, Kind, StringComparison.OrdinalIgnoreCase)))
            {
                if (Kind == "Runtime" && observation.Envelope != null &&
                    observation.Envelope.PayloadRef != null &&
                    observation.Envelope.PayloadRef.Mime == TCTRuntimeOwnerEvidence.Mime)
                    continue;
                if (observation.Envelope == null || observation.Envelope.PayloadRef == null ||
                    observation.Envelope.PayloadRef.Mime != TCTGraphRuntimeEvidence.Mime)
                {
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "NoEvidence：没有受支持的GraphRuntime结构化Owner数据 · " +
                            observation.Title,
                        Foreground = Brushes.DarkGoldenrod, TextWrapping = TextWrapping.Wrap });
                    continue;
                }
                try
                {
                    TCTTestArtifactStore store = new TCTTestArtifactStore(
                        ArchivedEvidenceRoot ?? TCTTestArtifactStore.RunRoot(result.RunId));
                    TCTObservationReplay.Read(result, store);
                    XDocument data = TCTGraphRuntimeEvidence.Read(store.Read(observation.Envelope.PayloadRef));
                    XElement root = data.Root;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "SHA256/Schema已验证 · Project " + (string)root.Attribute("projectId") +
                            " · Revision " + (string)root.Attribute("revision") +
                            " · Flow " + (string)root.Attribute("flowId"),
                        FontWeight = FontWeights.Bold, Margin = new Thickness(0, 7, 0, 9),
                        TextWrapping = TextWrapping.Wrap });
                    Render(data, observation);
                }
                catch (Exception ex) when (ex is InvalidDataException || ex is IOException ||
                    ex is System.Xml.XmlException || ex is UnauthorizedAccessException ||
                    ex is NotSupportedException)
                {
                    EvidenceProblem = ex.Message;
                    ContentPanel.Children.Add(new TextBlock {
                        Text = "GraphRuntime证据被拒绝：" + ex.Message,
                        Foreground = Brushes.DarkRed, TextWrapping = TextWrapping.Wrap });
                }
            }
            Observations(result, Kind);
            Assertions(result);
        }
    }
}
