using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq; // Use XDocument for modern XML parsing
using System.Xml; // Required for IXmlLineInfo
using System.IO; // Required for Path.GetFileName
using System.Drawing.Drawing2D; // Required for LinearGradientBrush

namespace DS1_FFX_Recolorer
{
    public partial class MainForm : Form
    {
        // Store the XML document in memory. We'll modify this object, not the file itself.
        private XDocument _xmlDocument;
        private string _filePath;
        private ToolTip _lineToolTip;
        // Declare the XNamespace at the class level to avoid scope conflicts.
        private readonly XNamespace _xsi = XNamespace.Get("http://www.w3.org/2001/XMLSchema-instance");

        // A new private field to hold the EffectID element for easy access.
        private XElement _effectIdElement;

        // A new helper class to hold a GroupBox and its corresponding XML line number.
        private class GroupBoxWithLineInfo
        {
            public GroupBox GroupBox { get; set; }
            public int LineNumber { get; set; }
        }

        // A custom class to hold all possible color element references for any action data type.
        private class ColorDataWrapper
        {
            public XElement BaseColorRElement { get; set; }
            public XElement BaseColorGElement { get; set; }
            public XElement BaseColorBElement { get; set; }
            public XElement Ds1rRElement { get; set; }
            public XElement Ds1rGElement { get; set; }
            public XElement Ds1rBElement { get; set; }
            public XElement Ds1rAElement { get; set; }
            public XElement Ds1rPElement { get; set; }
            public XElement ColorSequenceNode { get; set; }
            public XElement Unk9Element { get; set; }
            public XElement Unk16Element { get; set; }
            public XElement Unk17Element { get; set; }
            // A dedicated list for all unique keyframe times for this group box.
            public List<float> AllUniqueTimes { get; set; }
            // The current index in the AllUniqueTimes list.
            public int CurrentTimeIndex { get; set; }

            // New lists to hold all FloatTick elements for FloatSequences and ColorTicks for ColorSequences
            public List<XElement> BaseColorRTicks { get; set; } = new List<XElement>();
            public List<XElement> BaseColorGTicks { get; set; } = new List<XElement>();
            public List<XElement> BaseColorBTicks { get; set; } = new List<XElement>();
            public List<XElement> Ds1rATicks { get; set; } = new List<XElement>();
            public List<XElement> Ds1rPTicks { get; set; } = new List<XElement>();
            public List<XElement> ColorTicks { get; set; } = new List<XElement>();
        }

        // A new helper class to hold the color and time for the gradient drawing
        private class ColorGradientData
        {
            public Color Color { get; set; }
            public float Time { get; set; }
        }

        public MainForm()
        {
            InitializeComponent();
            _lineToolTip = new ToolTip();
        }

        // Event handler for the "Open..." menu item.
        private void openToolStripMenuItem_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*";
            openFileDialog.Title = "Select a DS1 FFX XML file";

            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                _filePath = openFileDialog.FileName;
                LoadAndDisplayXmlData(_filePath);
                saveAsToolStripMenuItem.Enabled = true;
            }
        }

        // Loads and parses the XML file, then binds the data to the UI elements.
        private void LoadAndDisplayXmlData(string filePath)
        {
            try
            {
                // Load with line info enabled for debugging.
                _xmlDocument = XDocument.Load(filePath, LoadOptions.SetLineInfo);
                if (_xmlDocument == null || _xmlDocument.Root == null)
                {
                    MessageBox.Show("Invalid XML file format.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Update the form's title with the file name.
                this.Text = $"{Path.GetFileName(filePath)} - DS1 FFX Recolorer";

                // Clear any existing controls from a previous load.
                flowLayoutPanel1.Controls.Clear();

                // Find the first EffectID element and store it.
                _effectIdElement = _xmlDocument.Descendants("EffectID").FirstOrDefault();
                if (_effectIdElement != null)
                {
                    // Populate the EffectID textbox with the value from the XML
                    this.effectIdTextBox.Text = _effectIdElement.Value;
                    this.effectIdTextBox.Enabled = true;
                }
                else
                {
                    this.effectIdTextBox.Text = "N/A";
                    this.effectIdTextBox.Enabled = false;
                }

                int colorGroupCount = 1;
                var groupBoxList = new List<GroupBoxWithLineInfo>();

                // --- Part 1: Handle various ActionData elements ---
                var actionDataElements = _xmlDocument.Descendants("ActionData").ToList();
                foreach (var actionData in actionDataElements)
                {
                    XAttribute xsiType = actionData.Attribute(_xsi + "type");
                    if (xsiType == null)
                    {
                        continue;
                    }

                    XElement rElement = null;
                    XElement gElement = null;
                    XElement bElement = null;
                    string elementType = xsiType.Value;

                    // A single data wrapper to hold all potential element references.
                    ColorDataWrapper dataWrapper = new ColorDataWrapper();

                    // Case 1: Particle2DActionData71 with Color2R, Color2G, Color2B
                    if (elementType == "Particle2DActionData71")
                    {
                        rElement = actionData.Element("Color2R");
                        gElement = actionData.Element("Color2G");
                        bElement = actionData.Element("Color2B");

                        if (rElement != null && gElement != null && bElement != null)
                        {
                            dataWrapper.BaseColorRElement = rElement;
                            dataWrapper.BaseColorGElement = gElement;
                            dataWrapper.BaseColorBElement = bElement;
                        }
                    }
                    // Case 2: FXActionData40 with Unk11_1, Unk11_2, Unk11_3
                    else if (elementType == "FXActionData40")
                    {
                        rElement = actionData.Element("Unk11_1");
                        gElement = actionData.Element("Unk11_2");
                        bElement = actionData.Element("Unk11_3");

                        if (rElement != null && gElement != null && bElement != null)
                        {
                            dataWrapper.BaseColorRElement = rElement;
                            dataWrapper.BaseColorGElement = gElement;
                            dataWrapper.BaseColorBElement = bElement;
                        }
                    }
                    // Case 3: FXActionData59 with Unk7_5, Unk7_6, Unk7_7
                    else if (elementType == "FXActionData59")
                    {
                        rElement = actionData.Element("Unk7_5");
                        gElement = actionData.Element("Unk7_6");
                        bElement = actionData.Element("Unk7_7");

                        if (rElement != null && gElement != null && bElement != null)
                        {
                            dataWrapper.BaseColorRElement = rElement;
                            dataWrapper.BaseColorGElement = gElement;
                            dataWrapper.BaseColorBElement = bElement;
                        }
                    }
                    // Case 4: Particle3DActionData108 with Color2R, Color2G, Color2B
                    else if (elementType == "Particle3DActionData108")
                    {
                        rElement = actionData.Element("Color2R");
                        gElement = actionData.Element("Color2G");
                        bElement = actionData.Element("Color2B");

                        if (rElement != null && gElement != null && bElement != null)
                        {
                            dataWrapper.BaseColorRElement = rElement;
                            dataWrapper.BaseColorGElement = gElement;
                            dataWrapper.BaseColorBElement = bElement;
                        }
                    }

                    // If we found a base color, create the group box.
                    if (dataWrapper.BaseColorRElement != null)
                    {
                        GroupBox groupBox = CreateColorGroupBox(actionData, dataWrapper, elementType, ref colorGroupCount);
                        groupBoxList.Add(new GroupBoxWithLineInfo { GroupBox = groupBox, LineNumber = ((IXmlLineInfo)actionData)?.LineNumber ?? 0 });
                    }
                }

                // --- Part 2: Handle all types of ColorSequenceNode elements, ensuring no duplicates ---
                var allColorSequenceNodes = _xmlDocument.Descendants("FXNode")
                    .Where(n => n.Attribute(_xsi + "type")?.Value?.StartsWith("ColorSequenceNode") == true)
                    .Distinct()
                    .ToList();

                foreach (var colorSequenceNode in allColorSequenceNodes)
                {
                    GroupBox groupBox = CreateColorSequenceGroupBox(colorSequenceNode, ref colorGroupCount);
                    if (groupBox != null)
                    {
                        groupBoxList.Add(new GroupBoxWithLineInfo { GroupBox = groupBox, LineNumber = ((IXmlLineInfo)colorSequenceNode)?.LineNumber ?? 0 });
                    }
                }

                // Sort the GroupBoxes by line number before adding them to the FlowLayoutPanel.
                var sortedGroupBoxes = groupBoxList.OrderBy(gb => gb.LineNumber).ToList();

                // Re-label the group boxes and add them to the flow panel.
                int newColorGroupCount = 1;
                foreach (var item in sortedGroupBoxes)
                {
                    // Update the title labels based on the new layout
                    item.GroupBox.Text = $"Color #{newColorGroupCount}";
                    newColorGroupCount++;

                    flowLayoutPanel1.Controls.Add(item.GroupBox);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"An error occurred while loading the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // Helper method to clamp a float value between 0 and 1 before converting to a byte.
        private int ClampColorValue(float value)
        {
            // Clamp the float to the 0-1 range, then multiply by 255.
            float clampedValue = Math.Max(0f, Math.Min(1f, value));
            return (int)(clampedValue * 255);
        }

        // Helper method to clamp a float value between 0 and 10 before converting to a byte.
        private int ClampTintValue(float value)
        {
            // Clamp the float to the 0-10 range, then divide by 10 and multiply by 255.
            float clampedValue = Math.Max(0f, Math.Min(10f, value));
            return (int)((clampedValue / 10f) * 255);
        }

        // Helper method to normalize and clamp color values for the tint preview
        private Color NormalizeAndClampColor(float r, float g, float b)
        {
            float maxVal = Math.Max(r, Math.Max(g, b));
            if (maxVal > 1.0f)
            {
                r /= maxVal;
                g /= maxVal;
                b /= maxVal;
            }

            int colorR = ClampColorValue(r);
            int colorG = ClampColorValue(g);
            int colorB = ClampColorValue(b);
            return Color.FromArgb(colorR, colorG, colorB);
        }

        // New helper method for linear interpolation.
        private float InterpolateFloat(float time, float time1, float value1, float time2, float value2)
        {
            if (time1 == time2) return value1; // Avoid division by zero
            return value1 + (value2 - value1) * ((time - time1) / (time2 - time1));
        }

        // Helper method to create a GroupBox for the common color types.
        private GroupBox CreateColorGroupBox(XElement mainElement, ColorDataWrapper dataWrapper, string elementType, ref int colorGroupCount)
        {
            GroupBox groupBox = new GroupBox();

            // Set a consistent, taller size for all GroupBoxes.
            groupBox.Size = new Size(410, 340);

            // Set the group box title and make it bold
            groupBox.Text = $"Color #{colorGroupCount++}";
            groupBox.Font = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Bold);

            // Create a non-bold font for all other labels
            var defaultFont = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Regular);

            groupBox.Padding = new Padding(10);
            groupBox.Tag = dataWrapper;

            int lineNumber = ((IXmlLineInfo)mainElement)?.LineNumber ?? 0;
            string tooltipText = $"Line {lineNumber} in XML";
            _lineToolTip.SetToolTip(groupBox, tooltipText);

            // Add the non-bold label for the element type
            Label nonBoldLabel = new Label();
            nonBoldLabel.Text = $"({elementType})";
            nonBoldLabel.Font = defaultFont; // Explicitly set to non-bold
            nonBoldLabel.Location = new Point(10, 25);
            nonBoldLabel.AutoSize = true;
            groupBox.Controls.Add(nonBoldLabel);

            // Check if base colors are FloatSequences
            XElement color2rElement = mainElement.Element("Color2R");
            if (color2rElement != null && color2rElement.Attribute(_xsi + "type")?.Value == "FloatSequence")
            {
                dataWrapper.BaseColorRTicks = color2rElement.Descendants("FloatTick").ToList();
                dataWrapper.BaseColorGTicks = mainElement.Element("Color2G").Descendants("FloatTick").ToList();
                dataWrapper.BaseColorBTicks = mainElement.Element("Color2B").Descendants("FloatTick").ToList();
            }

            // Set up the base color controls with the new TextBoxes
            SetupBaseColorControls(groupBox, dataWrapper, new Point(16, 50));

            // A new panel for the color gradient visualization
            Panel gradientPanel = new Panel();
            gradientPanel.Location = new Point(16, 160);
            gradientPanel.Size = new Size(183, 15);
            gradientPanel.BorderStyle = BorderStyle.FixedSingle;
            gradientPanel.Name = "GradientPanel";
            gradientPanel.Paint += new PaintEventHandler(GradientPanel_Paint);
            groupBox.Controls.Add(gradientPanel);

            // Now, check for and add the DS1RData values if they exist within the same main element.
            XElement ds1rDataElement = mainElement.Element("DS1RData");
            if (ds1rDataElement != null)
            {
                XElement ds1r_rElement = ds1rDataElement.Element("Unk1");
                XElement ds1r_gElement = ds1rDataElement.Element("Unk2");
                XElement ds1r_bElement = ds1rDataElement.Element("Unk3");
                XElement ds1r_aElement = ds1rDataElement.Element("Unk4");
                XElement ds1r_pElement = ds1rDataElement.Element("Unk5");

                dataWrapper.Ds1rRElement = ds1r_rElement;
                dataWrapper.Ds1rGElement = ds1r_gElement;
                dataWrapper.Ds1rBElement = ds1r_bElement;
                dataWrapper.Ds1rAElement = ds1r_aElement;
                dataWrapper.Ds1rPElement = ds1r_pElement;

                if (ds1r_aElement?.Attribute(_xsi + "type")?.Value == "FloatSequence")
                {
                    dataWrapper.Ds1rATicks = ds1r_aElement.Descendants("FloatTick").ToList();
                }
                if (ds1r_pElement?.Attribute(_xsi + "type")?.Value == "FloatSequence")
                {
                    dataWrapper.Ds1rPTicks = ds1r_pElement.Descendants("FloatTick").ToList();
                }
            }

            // Get all unique times and store them in the data wrapper
            dataWrapper.AllUniqueTimes = GetAllUniqueTimes(dataWrapper).ToList();
            dataWrapper.CurrentTimeIndex = 0; // Initialize at the first time

            // Add the timeline controls if there are any keyframes
            if (dataWrapper.AllUniqueTimes.Any())
            {
                CreateTimelineControls(groupBox, dataWrapper, new Point(16, 185));
            }

            // Add the tint color controls and alpha/power textboxes
            SetupTintColorControls(groupBox, dataWrapper, new Point(16, 220));
            SetupDs1rDataAndBlendModeControls(groupBox, dataWrapper, mainElement, new Point(200, 220));

            // Initialize the UI with the first tick
            UpdateUiForCurrentTime(groupBox, dataWrapper.AllUniqueTimes.FirstOrDefault());

            return groupBox;
        }

        // Helper method to create a GroupBox for ColorSequenceNode types.
        private GroupBox CreateColorSequenceGroupBox(XElement colorSequenceNode, ref int colorGroupCount)
        {
            string elementType = colorSequenceNode.Attribute(_xsi + "type")?.Value;

            // A single data wrapper to hold all potential element references.
            ColorDataWrapper dataWrapper = new ColorDataWrapper { ColorSequenceNode = colorSequenceNode };
            dataWrapper.ColorTicks = colorSequenceNode.Descendants("ColorTick").ToList();

            GroupBox groupBox = new GroupBox();

            // Set a consistent, taller size for all GroupBoxes.
            groupBox.Size = new Size(410, 340);

            // Set the group box title and make it bold
            groupBox.Text = $"Color #{colorGroupCount++}";
            groupBox.Font = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Bold);

            // Create a non-bold font for all other labels
            var defaultFont = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Regular);

            groupBox.Padding = new Padding(10);
            groupBox.Tag = dataWrapper;

            int lineNumber = ((IXmlLineInfo)colorSequenceNode)?.LineNumber ?? 0;
            string tooltipText = $"Line {lineNumber} in XML";
            _lineToolTip.SetToolTip(groupBox, tooltipText);

            // Add the non-bold label for the element type
            Label nonBoldLabel = new Label();
            nonBoldLabel.Text = $"({elementType})";
            nonBoldLabel.Font = defaultFont; // Explicitly set to non-bold
            nonBoldLabel.Location = new Point(10, 25);
            nonBoldLabel.AutoSize = true;
            groupBox.Controls.Add(nonBoldLabel);

            // Add the base color controls with the new TextBoxes
            SetupBaseColorControls(groupBox, dataWrapper, new Point(16, 50));

            // A new panel for the color gradient visualization
            Panel gradientPanel = new Panel();
            gradientPanel.Location = new Point(16, 160);
            gradientPanel.Size = new Size(183, 15);
            gradientPanel.BorderStyle = BorderStyle.FixedSingle;
            gradientPanel.Name = "GradientPanel";
            gradientPanel.Paint += new PaintEventHandler(GradientPanel_Paint);
            groupBox.Controls.Add(gradientPanel);

            // Now, check for and add the DS1RData values if they exist within the same main element.
            XElement ds1rDataElement = colorSequenceNode.Element("DS1RData");
            if (ds1rDataElement != null)
            {
                XElement ds1r_rElement = ds1rDataElement.Element("Unk1");
                XElement ds1r_gElement = ds1rDataElement.Element("Unk2");
                XElement ds1r_bElement = ds1rDataElement.Element("Unk3");
                XElement ds1r_aElement = ds1rDataElement.Element("Unk4");
                XElement ds1r_pElement = ds1rDataElement.Element("Unk5");

                dataWrapper.Ds1rRElement = ds1r_rElement;
                dataWrapper.Ds1rGElement = ds1r_gElement;
                dataWrapper.Ds1rBElement = ds1r_bElement;
                dataWrapper.Ds1rAElement = ds1r_aElement;
                dataWrapper.Ds1rPElement = ds1r_pElement;

                if (ds1r_aElement?.Attribute(_xsi + "type")?.Value == "FloatSequence")
                {
                    dataWrapper.Ds1rATicks = ds1r_aElement.Descendants("FloatTick").ToList();
                }
                if (ds1r_pElement?.Attribute(_xsi + "type")?.Value == "FloatSequence")
                {
                    dataWrapper.Ds1rPTicks = ds1r_pElement.Descendants("FloatTick").ToList();
                }
            }

            // Get all unique times and store them in the data wrapper
            dataWrapper.AllUniqueTimes = GetAllUniqueTimes(dataWrapper).ToList();
            dataWrapper.CurrentTimeIndex = 0; // Initialize at the first time

            // Add the timeline controls if there are any keyframes
            if (dataWrapper.AllUniqueTimes.Any())
            {
                CreateTimelineControls(groupBox, dataWrapper, new Point(16, 185));
            }

            // Add the tint color controls and alpha/power textboxes
            SetupTintColorControls(groupBox, dataWrapper, new Point(16, 220));
            SetupDs1rDataAndBlendModeControls(groupBox, dataWrapper, colorSequenceNode, new Point(200, 220));

            // Initialize the UI with the first tick
            UpdateUiForCurrentTime(groupBox, dataWrapper.AllUniqueTimes.FirstOrDefault());

            return groupBox;
        }

        // Gathers all unique time values from all sequences in a ColorDataWrapper
        private SortedSet<float> GetAllUniqueTimes(ColorDataWrapper dataWrapper)
        {
            var times = new SortedSet<float>();

            // Collect times from Base Color sequences
            if (dataWrapper.ColorTicks.Any())
            {
                foreach (var tick in dataWrapper.ColorTicks)
                {
                    if (float.TryParse(tick.Attribute("Time")?.Value, out float time))
                    {
                        times.Add(time);
                    }
                }
            }
            if (dataWrapper.BaseColorRTicks.Any())
            {
                foreach (var tick in dataWrapper.BaseColorRTicks)
                {
                    if (float.TryParse(tick.Attribute("Time")?.Value, out float time))
                    {
                        times.Add(time);
                    }
                }
            }

            // Collect times from Alpha and Power sequences
            if (dataWrapper.Ds1rATicks.Any())
            {
                foreach (var tick in dataWrapper.Ds1rATicks)
                {
                    if (float.TryParse(tick.Attribute("Time")?.Value, out float time))
                    {
                        times.Add(time);
                    }
                }
            }
            if (dataWrapper.Ds1rPTicks.Any())
            {
                foreach (var tick in dataWrapper.Ds1rPTicks)
                {
                    if (float.TryParse(tick.Attribute("Time")?.Value, out float time))
                    {
                        times.Add(time);
                    }
                }
            }

            // If no unique times were found, add a default time of 0
            if (!times.Any())
            {
                times.Add(0f);
            }

            return times;
        }

        // Helper method to create the timeline controls (label and buttons)
        private void CreateTimelineControls(GroupBox groupBox, ColorDataWrapper dataWrapper, Point startLocation)
        {
            var defaultFont = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Regular);

            // Timeline Label
            Label timelineLabel = new Label();
            timelineLabel.Name = "TimelineLabel";
            timelineLabel.Text = $"Time: 0.00s"; // Initial text
            timelineLabel.Location = startLocation;
            timelineLabel.Font = defaultFont;
            timelineLabel.AutoSize = true;
            groupBox.Controls.Add(timelineLabel);

            // Previous Button
            Button prevButton = new Button();
            prevButton.Name = "PreviousButton";
            prevButton.Text = "<";
            prevButton.Location = new Point(startLocation.X + 85, startLocation.Y - 3); // Position to the right of the label
            prevButton.Size = new Size(30, 25);
            prevButton.Click += new EventHandler(PreviousButton_Click);
            groupBox.Controls.Add(prevButton);

            // Next Button
            Button nextButton = new Button();
            nextButton.Name = "NextButton";
            nextButton.Text = ">";
            nextButton.Location = new Point(prevButton.Right + 5, startLocation.Y - 3);
            nextButton.Size = new Size(30, 25);
            nextButton.Click += new EventHandler(NextButton_Click);
            groupBox.Controls.Add(nextButton);

            // Display current time index
            Label timeIndexLabel = new Label();
            timeIndexLabel.Name = "TimeIndexLabel";
            timeIndexLabel.Text = $"1/{dataWrapper.AllUniqueTimes.Count}";
            timeIndexLabel.Location = new Point(nextButton.Right + 5, startLocation.Y + 2);
            timeIndexLabel.Font = defaultFont;
            timeIndexLabel.AutoSize = true;
            groupBox.Controls.Add(timeIndexLabel);
        }

        // Handles the click event for the previous button.
        private void PreviousButton_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            GroupBox parentGroupBox = button.Parent as GroupBox;
            if (parentGroupBox?.Tag is ColorDataWrapper dataWrapper)
            {
                if (dataWrapper.CurrentTimeIndex > 0)
                {
                    dataWrapper.CurrentTimeIndex--;
                    float newTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];
                    UpdateUiForCurrentTime(parentGroupBox, newTime);
                }
            }
        }

        // Handles the click event for the next button.
        private void NextButton_Click(object sender, EventArgs e)
        {
            Button button = (Button)sender;
            GroupBox parentGroupBox = button.Parent as GroupBox;
            if (parentGroupBox?.Tag is ColorDataWrapper dataWrapper)
            {
                if (dataWrapper.CurrentTimeIndex < dataWrapper.AllUniqueTimes.Count - 1)
                {
                    dataWrapper.CurrentTimeIndex++;
                    float newTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];
                    UpdateUiForCurrentTime(parentGroupBox, newTime);
                }
            }
        }

        // This is a new method that updates all UI elements within a group box based on the current time.
        private void UpdateUiForCurrentTime(GroupBox groupBox, float time)
        {
            if (groupBox.Tag is ColorDataWrapper dataWrapper)
            {
                float baseR = 0f, baseG = 0f, baseB = 0f;
                float tintR = 0f, tintG = 0f, tintB = 0f;
                float alpha = 0f, power = 0f;

                bool isBaseKeyframed = false;
                bool isAlphaKeyframed = false;
                bool isPowerKeyframed = false;

                // --- 1. Handle Base Color values ---
                if (dataWrapper.ColorSequenceNode != null)
                {
                    // Case: ColorSequenceNode
                    if (dataWrapper.ColorTicks.Any())
                    {
                        var tick = dataWrapper.ColorTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == time);
                        if (tick != null)
                        {
                            baseR = (float?)tick.Attribute("R") ?? 0f;
                            baseG = (float?)tick.Attribute("G") ?? 0f;
                            baseB = (float?)tick.Attribute("B") ?? 0f;
                            isBaseKeyframed = true;
                        }
                        else
                        {
                            // Interpolate the value
                            var beforeTick = dataWrapper.ColorTicks.LastOrDefault(t => (float?)t.Attribute("Time") < time);
                            var afterTick = dataWrapper.ColorTicks.FirstOrDefault(t => (float?)t.Attribute("Time") > time);

                            if (beforeTick != null && afterTick != null)
                            {
                                float time1 = (float)beforeTick.Attribute("Time");
                                float r1 = (float)beforeTick.Attribute("R");
                                float g1 = (float)beforeTick.Attribute("G");
                                float b1 = (float)beforeTick.Attribute("B");

                                float time2 = (float)afterTick.Attribute("Time");
                                float r2 = (float)afterTick.Attribute("R");
                                float g2 = (float)afterTick.Attribute("G");
                                float b2 = (float)afterTick.Attribute("B");

                                baseR = InterpolateFloat(time, time1, r1, time2, r2);
                                baseG = InterpolateFloat(time, time1, g1, time2, g2);
                                baseB = InterpolateFloat(time, time1, b1, time2, b2);
                            }
                            else if (beforeTick != null)
                            {
                                baseR = (float)beforeTick.Attribute("R");
                                baseG = (float)beforeTick.Attribute("G");
                                baseB = (float)beforeTick.Attribute("B");
                            }
                        }
                    }
                }
                else if (dataWrapper.BaseColorRElement != null)
                {
                    // Case: FloatSequence for Base Color
                    if (dataWrapper.BaseColorRTicks.Any())
                    {
                        var rTick = dataWrapper.BaseColorRTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == time);
                        var gTick = dataWrapper.BaseColorGTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == time);
                        var bTick = dataWrapper.BaseColorBTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == time);

                        if (rTick != null) { baseR = (float)rTick.Attribute("Value"); isBaseKeyframed = true; }
                        else { baseR = InterpolateFloatValue(dataWrapper.BaseColorRTicks, time); }

                        if (gTick != null) { baseG = (float)gTick.Attribute("Value"); }
                        else { baseG = InterpolateFloatValue(dataWrapper.BaseColorGTicks, time); }

                        if (bTick != null) { baseB = (float)bTick.Attribute("Value"); }
                        else { baseB = InterpolateFloatValue(dataWrapper.BaseColorBTicks, time); }
                    }
                    else // ConstFloat case for Base Color
                    {
                        baseR = (float?)dataWrapper.BaseColorRElement.Attribute("Value") ?? 0f;
                        baseG = (float?)dataWrapper.BaseColorGElement.Attribute("Value") ?? 0f;
                        baseB = (float?)dataWrapper.BaseColorBElement.Attribute("Value") ?? 0f;
                        isBaseKeyframed = true; // Always a keyframe for ConstFloat
                    }
                }


                // --- 2. Handle DS1RData values (Tint, Alpha, Power) ---
                if (dataWrapper.Ds1rRElement != null)
                {
                    // Tint R, G, B are always ConstFloats.
                    tintR = (float?)dataWrapper.Ds1rRElement.Attribute("Value") ?? 0f;
                    tintG = (float?)dataWrapper.Ds1rGElement.Attribute("Value") ?? 0f;
                    tintB = (float?)dataWrapper.Ds1rBElement.Attribute("Value") ?? 0f;

                    // Alpha
                    if (dataWrapper.Ds1rATicks.Any())
                    {
                        var aTick = dataWrapper.Ds1rATicks.FirstOrDefault(t => (float?)t.Attribute("Time") == time);
                        if (aTick != null) { alpha = (float)aTick.Attribute("Value"); isAlphaKeyframed = true; }
                        else { alpha = InterpolateFloatValue(dataWrapper.Ds1rATicks, time); }
                    }
                    else // ConstFloat case
                    {
                        alpha = (float?)dataWrapper.Ds1rAElement.Attribute("Value") ?? 0f;
                        isAlphaKeyframed = true;
                    }

                    // Power
                    if (dataWrapper.Ds1rPTicks.Any())
                    {
                        var pTick = dataWrapper.Ds1rPTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == time);
                        if (pTick != null) { power = (float)pTick.Attribute("Value"); isPowerKeyframed = true; }
                        else { power = InterpolateFloatValue(dataWrapper.Ds1rPTicks, time); }
                    }
                    else // ConstFloat case
                    {
                        power = (float?)dataWrapper.Ds1rPElement.Attribute("Value") ?? 0f;
                        isPowerKeyframed = true;
                    }

                    // Now update the UI with the retrieved values and keyframe flags
                    UpdateBaseColorControls(groupBox, baseR, baseG, baseB, isBaseKeyframed);
                    UpdateDs1rDataControls(groupBox, tintR, tintG, tintB, alpha, power, true, isAlphaKeyframed, isPowerKeyframed);
                }
                else
                {
                    // Update base color controls without DS1RData logic.
                    UpdateBaseColorControls(groupBox, baseR, baseG, baseB, isBaseKeyframed);
                }

                // Update the timeline label and invalidating the gradient panel
                var timelineLabel = groupBox.Controls.Find("TimelineLabel", true).FirstOrDefault() as Label;
                if (timelineLabel != null)
                {
                    timelineLabel.Text = $"Time: {time:F2}s";
                }

                var timeIndexLabel = groupBox.Controls.Find("TimeIndexLabel", true).FirstOrDefault() as Label;
                if (timeIndexLabel != null)
                {
                    timeIndexLabel.Text = $"{dataWrapper.CurrentTimeIndex + 1}/{dataWrapper.AllUniqueTimes.Count}";
                }

                var gradientPanel = groupBox.Controls.Find("GradientPanel", true).FirstOrDefault();
                if (gradientPanel != null)
                {
                    gradientPanel.Invalidate();
                }
            }
        }

        // Interpolates a value for a FloatSequence at a given time.
        private float InterpolateFloatValue(List<XElement> ticks, float time)
        {
            if (!ticks.Any()) return 0f;
            if (ticks.Count == 1) return (float?)ticks.First().Attribute("Value") ?? 0f;

            var beforeTick = ticks.LastOrDefault(t => (float?)t.Attribute("Time") < time);
            var afterTick = ticks.FirstOrDefault(t => (float?)t.Attribute("Time") > time);

            if (beforeTick == null && afterTick != null) return (float)afterTick.Attribute("Value");
            if (beforeTick != null && afterTick == null) return (float)beforeTick.Attribute("Value");
            if (beforeTick == null && afterTick == null) return 0f;

            float time1 = (float)beforeTick.Attribute("Time");
            float value1 = (float)beforeTick.Attribute("Value");
            float time2 = (float)afterTick.Attribute("Time");
            float value2 = (float)afterTick.Attribute("Value");

            return InterpolateFloat(time, time1, value1, time2, value2);
        }

        // Handles the Paint event for the gradient panel
        private void GradientPanel_Paint(object sender, PaintEventArgs e)
        {
            Panel gradientPanel = sender as Panel;
            GroupBox parentGroupBox = gradientPanel?.Parent as GroupBox;

            if (parentGroupBox?.Tag is ColorDataWrapper dataWrapper)
            {
                var gradientData = new List<ColorGradientData>();

                // Get color and time data for all ticks
                if (dataWrapper.ColorSequenceNode != null)
                {
                    var colorTicks = dataWrapper.ColorSequenceNode.Descendants("ColorTick").ToList();
                    foreach (var tick in colorTicks)
                    {
                        float time = (float?)tick.Attribute("Time") ?? 0f;
                        float r = (float?)tick.Attribute("R") ?? 0f;
                        float g = (float?)tick.Attribute("G") ?? 0f;
                        float b = (float?)tick.Attribute("B") ?? 0f;
                        gradientData.Add(new ColorGradientData { Color = Color.FromArgb(ClampColorValue(r), ClampColorValue(g), ClampColorValue(b)), Time = time });
                    }
                }
                else if (dataWrapper.BaseColorRTicks.Any())
                {
                    var rTicks = dataWrapper.BaseColorRElement.Descendants("FloatTick").ToList();
                    var gTicks = dataWrapper.BaseColorGElement.Descendants("FloatTick").ToList();
                    var bTicks = dataWrapper.BaseColorBElement.Descendants("FloatTick").ToList();

                    for (int i = 0; i < rTicks.Count; i++)
                    {
                        float time = (float?)rTicks[i].Attribute("Time") ?? 0f;
                        float r = (float?)rTicks[i].Attribute("Value") ?? 0f;
                        float g = (float?)gTicks[i].Attribute("Value") ?? 0f;
                        float b = (float?)bTicks[i].Attribute("Value") ?? 0f;
                        gradientData.Add(new ColorGradientData { Color = Color.FromArgb(ClampColorValue(r), ClampColorValue(g), ClampColorValue(b)), Time = time });
                    }
                }
                else // ConstFloat case
                {
                    float r = (float?)dataWrapper.BaseColorRElement?.Attribute("Value") ?? 0f;
                    float g = (float?)dataWrapper.BaseColorGElement?.Attribute("Value") ?? 0f;
                    float b = (float?)dataWrapper.BaseColorBElement?.Attribute("Value") ?? 0f;
                    gradientData.Add(new ColorGradientData { Color = Color.FromArgb(ClampColorValue(r), ClampColorValue(g), ClampColorValue(b)), Time = 0f });
                    gradientData.Add(new ColorGradientData { Color = Color.FromArgb(ClampColorValue(r), ClampColorValue(g), ClampColorValue(b)), Time = 1f });
                }

                if (gradientData.Count > 1)
                {
                    // Find the maximum time value to normalize the positions.
                    float maxTime = gradientData.Max(d => d.Time);

                    // Create the ColorBlend object
                    var colorBlend = new ColorBlend(gradientData.Count);
                    colorBlend.Colors = gradientData.Select(d => d.Color).ToArray();
                    colorBlend.Positions = gradientData.Select(d => maxTime > 0 ? d.Time / maxTime : 0.0f).ToArray();

                    // Create the brush and apply the color blend
                    using (var brush = new LinearGradientBrush(
                        gradientPanel.ClientRectangle,
                        Color.Black, // dummy start color
                        Color.Black, // dummy end color
                        LinearGradientMode.Horizontal))
                    {
                        brush.InterpolationColors = colorBlend;
                        e.Graphics.FillRectangle(brush, gradientPanel.ClientRectangle);
                    }
                }
                else if (gradientData.Count == 1)
                {
                    // For a single color, just fill the entire panel with that color.
                    using (SolidBrush brush = new SolidBrush(gradientData[0].Color))
                    {
                        e.Graphics.FillRectangle(brush, gradientPanel.ClientRectangle);
                    }
                }
            }
        }


        // Updates the base color textboxes and panel.
        private void UpdateBaseColorControls(GroupBox groupBox, float r, float g, float b, bool isKeyframed)
        {
            TextBox rTextBox = groupBox.Controls.Find("baseRTextBox", true).FirstOrDefault() as TextBox;
            TextBox gTextBox = groupBox.Controls.Find("baseGTextBox", true).FirstOrDefault() as TextBox;
            TextBox bTextBox = groupBox.Controls.Find("baseBTextBox", true).FirstOrDefault() as TextBox;
            Panel panel = groupBox.Controls.Find("baseColorPanel", true).FirstOrDefault() as Panel;

            if (rTextBox != null)
            {
                rTextBox.Text = $"{r:F4}";
                rTextBox.Enabled = isKeyframed;
                rTextBox.BackColor = Color.White; // Reset to default background
            }
            if (gTextBox != null)
            {
                gTextBox.Text = $"{g:F4}";
                gTextBox.Enabled = isKeyframed;
                gTextBox.BackColor = Color.White;
            }
            if (bTextBox != null)
            {
                bTextBox.Text = $"{b:F4}";
                bTextBox.Enabled = isKeyframed;
                bTextBox.BackColor = Color.White;
            }
            if (panel != null) panel.BackColor = Color.FromArgb(ClampColorValue(r), ClampColorValue(g), ClampColorValue(b));
        }

        // Updates the DS1RData textboxes and panel.
        private void UpdateDs1rDataControls(GroupBox groupBox, float r, float g, float b, float a, float p, bool isTintKeyframed, bool isAlphaKeyframed, bool isPowerKeyframed)
        {
            TextBox rTextBox = groupBox.Controls.Find("tintRTextBox", true).FirstOrDefault() as TextBox;
            TextBox gTextBox = groupBox.Controls.Find("tintGTextBox", true).FirstOrDefault() as TextBox;
            TextBox bTextBox = groupBox.Controls.Find("tintBTextBox", true).FirstOrDefault() as TextBox;
            Panel panel = groupBox.Controls.Find("tintColorPanel", true).FirstOrDefault() as Panel;
            TextBox alphaTextBox = groupBox.Controls.Find("Ds1rAlphaTextBox", true).FirstOrDefault() as TextBox;
            TextBox powerTextBox = groupBox.Controls.Find("Ds1rPowerTextBox", true)
                                          .FirstOrDefault() as TextBox;

            if (rTextBox != null)
            {
                rTextBox.Text = $"{r:F4}";
                rTextBox.Enabled = isTintKeyframed;
                rTextBox.BackColor = Color.White;
            }
            if (gTextBox != null)
            {
                gTextBox.Text = $"{g:F4}";
                gTextBox.Enabled = isTintKeyframed;
                gTextBox.BackColor = Color.White;
            }
            if (bTextBox != null)
            {
                bTextBox.Text = $"{b:F4}";
                bTextBox.Enabled = isTintKeyframed;
                bTextBox.BackColor = Color.White;
            }

            if (panel != null) panel.BackColor = NormalizeAndClampColor(r, g, b);
            if (alphaTextBox != null)
            {
                alphaTextBox.Text = $"{a:F4}";
                alphaTextBox.Enabled = isAlphaKeyframed;
                alphaTextBox.BackColor = isAlphaKeyframed ? SystemColors.Window : SystemColors.Control;
            }
            if (powerTextBox != null)
            {
                powerTextBox.Text = $"{p:F4}";
                powerTextBox.Enabled = isPowerKeyframed;
                powerTextBox.BackColor = isPowerKeyframed ? SystemColors.Window : SystemColors.Control;
            }
        }

        // Handles the SelectedIndexChanged event for the blend mode combo box
        private void BlendModeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBox comboBox = (ComboBox)sender;
            GroupBox parentGroupBox = (GroupBox)comboBox.Tag;

            if (parentGroupBox.Tag is ColorDataWrapper dataWrapper)
            {
                string selectedMode = comboBox.SelectedItem?.ToString();

                if (dataWrapper.Unk16Element != null && dataWrapper.Unk17Element != null)
                {
                    if (selectedMode == "Add")
                    {
                        dataWrapper.Unk16Element.Value = "-1";
                        dataWrapper.Unk17Element.Value = "-1";
                    }
                    else if (selectedMode == "Subtract")
                    {
                        dataWrapper.Unk16Element.Value = "-2";
                        dataWrapper.Unk17Element.Value = "-2";
                    }
                }
            }
        }

        // Handles the Leave event for the new Base Color R, G, B text boxes
        private void BaseColorTextBox_Leave(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            GroupBox parentGroupBox = (GroupBox)textBox.Tag;
            if (parentGroupBox.Tag is ColorDataWrapper dataWrapper)
            {
                if (float.TryParse(textBox.Text, out float newValue))
                {
                    newValue = Math.Max(0f, Math.Min(1f, newValue));

                    float selectedTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];

                    if (dataWrapper.ColorSequenceNode != null)
                    {
                        var tick = dataWrapper.ColorTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == selectedTime);
                        if (tick != null)
                        {
                            if (textBox.Name.EndsWith("RTextBox"))
                            {
                                tick.SetAttributeValue("R", newValue.ToString("F4"));
                            }
                            else if (textBox.Name.EndsWith("GTextBox"))
                            {
                                tick.SetAttributeValue("G", newValue.ToString("F4"));
                            }
                            else if (textBox.Name.EndsWith("BTextBox"))
                            {
                                tick.SetAttributeValue("B", newValue.ToString("F4"));
                            }
                        }
                    }
                    else
                    {
                        XElement elementToUpdate = null;
                        if (textBox.Name.EndsWith("RTextBox")) elementToUpdate = dataWrapper.BaseColorRElement;
                        else if (textBox.Name.EndsWith("GTextBox")) elementToUpdate = dataWrapper.BaseColorGElement;
                        else if (textBox.Name.EndsWith("BTextBox")) elementToUpdate = dataWrapper.BaseColorBElement;

                        if (elementToUpdate != null)
                        {
                            UpdateXmlDocument(elementToUpdate, newValue, selectedTime);
                        }
                    }
                    UpdateUiForCurrentTime(parentGroupBox, selectedTime);
                }
                else
                {
                    MessageBox.Show("Invalid number format. Please enter a decimal number between 0 and 1.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    float selectedTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];
                    UpdateUiForCurrentTime(parentGroupBox, selectedTime); // Revert to the last valid value
                }
            }
        }

        // Handles the Leave event for the new Tint Color R, G, B text boxes
        private void TintColorTextBox_Leave(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            GroupBox parentGroupBox = (GroupBox)textBox.Tag;
            if (parentGroupBox.Tag is ColorDataWrapper dataWrapper)
            {
                if (float.TryParse(textBox.Text, out float newValue))
                {
                    newValue = Math.Max(0f, Math.Min(10f, newValue));

                    XElement elementToUpdate = null;
                    if (textBox.Name.EndsWith("RTextBox")) elementToUpdate = dataWrapper.Ds1rRElement;
                    else if (textBox.Name.EndsWith("GTextBox")) elementToUpdate = dataWrapper.Ds1rGElement;
                    else if (textBox.Name.EndsWith("BTextBox")) elementToUpdate = dataWrapper.Ds1rBElement;

                    if (elementToUpdate != null)
                    {
                        UpdateXmlDocument(elementToUpdate, newValue, 0); // Tint is always a ConstFloat
                    }
                    UpdateUiForCurrentTime(parentGroupBox, dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex]);
                }
                else
                {
                    MessageBox.Show("Invalid number format. Please enter a decimal number between 0 and 10.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    UpdateUiForCurrentTime(parentGroupBox, dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex]); // Revert to the last valid value
                }
            }
        }

        // Handles the click event for the color preview panels.
        private void ColorPreviewPanel_Click(object sender, EventArgs e)
        {
            Panel clickedPanel = (Panel)sender;
            GroupBox parentGroupBox = (GroupBox)clickedPanel.Tag;

            if (parentGroupBox.Tag is ColorDataWrapper dataWrapper)
            {
                float selectedTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];

                // This handles all types of base colors.
                if (clickedPanel.Name == "baseColorPanel")
                {
                    float r = 0f, g = 0f, b = 0f;

                    // Get the color values based on the current time.
                    if (dataWrapper.ColorSequenceNode != null)
                    {
                        var tick = dataWrapper.ColorTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == selectedTime);
                        if (tick != null)
                        {
                            r = (float?)tick.Attribute("R") ?? 0f;
                            g = (float?)tick.Attribute("G") ?? 0f;
                            b = (float?)tick.Attribute("B") ?? 0f;
                        }
                        else // Use interpolated values
                        {
                            var beforeTick = dataWrapper.ColorTicks.LastOrDefault(t => (float?)t.Attribute("Time") < selectedTime);
                            var afterTick = dataWrapper.ColorTicks.FirstOrDefault(t => (float?)t.Attribute("Time") > selectedTime);

                            if (beforeTick != null && afterTick != null)
                            {
                                float time1 = (float)beforeTick.Attribute("Time");
                                float r1 = (float)beforeTick.Attribute("R");
                                float g1 = (float)beforeTick.Attribute("G");
                                float b1 = (float)beforeTick.Attribute("B");
                                float time2 = (float)afterTick.Attribute("Time");
                                float r2 = (float)afterTick.Attribute("R");
                                float g2 = (float)afterTick.Attribute("G");
                                float b2 = (float)afterTick.Attribute("B");
                                r = InterpolateFloat(selectedTime, time1, r1, time2, r2);
                                g = InterpolateFloat(selectedTime, time1, g1, time2, g2);
                                b = InterpolateFloat(selectedTime, time1, b1, time2, b2);
                            }
                            else if (beforeTick != null)
                            {
                                r = (float)beforeTick.Attribute("R");
                                g = (float)beforeTick.Attribute("G");
                                b = (float)beforeTick.Attribute("B");
                            }
                        }
                    }
                    else if (dataWrapper.BaseColorRElement != null)
                    {
                        var rTick = dataWrapper.BaseColorRTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == selectedTime);
                        var gTick = dataWrapper.BaseColorGTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == selectedTime);
                        var bTick = dataWrapper.BaseColorBTicks.FirstOrDefault(t => (float?)t.Attribute("Time") == selectedTime);

                        r = rTick != null ? (float)rTick.Attribute("Value") : InterpolateFloatValue(dataWrapper.BaseColorRTicks, selectedTime);
                        g = gTick != null ? (float)gTick.Attribute("Value") : InterpolateFloatValue(dataWrapper.BaseColorGTicks, selectedTime);
                        b = bTick != null ? (float)bTick.Attribute("Value") : InterpolateFloatValue(dataWrapper.BaseColorBTicks, selectedTime);
                    }
                    else // ConstFloat case
                    {
                        r = (float?)dataWrapper.BaseColorRElement.Attribute("Value") ?? 0f;
                        g = (float?)dataWrapper.BaseColorGElement.Attribute("Value") ?? 0f;
                        b = (float?)dataWrapper.BaseColorBElement.Attribute("Value") ?? 0f;
                    }

                    colorDialog1.Color = Color.FromArgb(ClampColorValue(r), ClampColorValue(g), ClampColorValue(b));

                    if (colorDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Color newColor = colorDialog1.Color;
                        float newR = (float)newColor.R / 255;
                        float newG = (float)newColor.G / 255;
                        float newB = (float)newColor.B / 255;

                        // Update the XML document at the current time
                        if (dataWrapper.ColorSequenceNode != null)
                        {
                            UpdateXmlDocumentForColorSequenceNode(dataWrapper.ColorSequenceNode, newR, newG, newB, selectedTime);
                        }
                        else
                        {
                            UpdateXmlDocument(dataWrapper.BaseColorRElement, newR, selectedTime);
                            UpdateXmlDocument(dataWrapper.BaseColorGElement, newG, selectedTime);
                            UpdateXmlDocument(dataWrapper.BaseColorBElement, newB, selectedTime);
                        }
                        // Then update the UI
                        UpdateUiForCurrentTime(parentGroupBox, selectedTime);
                    }
                }
                // DS1RData Tint colors
                else if (clickedPanel.Name == "tintColorPanel")
                {
                    // Tint R, G, B are always ConstFloats.
                    float r = (float?)dataWrapper.Ds1rRElement.Attribute("Value") ?? 0f;
                    float g = (float?)dataWrapper.Ds1rGElement.Attribute("Value") ?? 0f;
                    float b = (float?)dataWrapper.Ds1rBElement.Attribute("Value") ?? 0f;

                    colorDialog1.Color = NormalizeAndClampColor(r, g, b);

                    if (colorDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Color newColor = colorDialog1.Color;
                        float newR = (float)newColor.R / 255 * 10f; // Scale to the 0-10 range
                        float newG = (float)newColor.G / 255 * 10f; // Scale to the 0-10 range
                        float newB = (float)newColor.B / 255 * 10f; // Scale to the 0-10 range

                        UpdateXmlDocument(dataWrapper.Ds1rRElement, newR, 0);
                        UpdateXmlDocument(dataWrapper.Ds1rGElement, newG, 0);
                        UpdateXmlDocument(dataWrapper.Ds1rBElement, newB, 0);
                        UpdateUiForCurrentTime(parentGroupBox, selectedTime);
                    }
                }
            }
        }

        // Handles the Leave event for the DS1RData text boxes
        private void Ds1rTextBox_Leave(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            GroupBox parentGroupBox = (GroupBox)textBox.Tag;
            if (parentGroupBox.Tag is ColorDataWrapper dataWrapper)
            {
                if (float.TryParse(textBox.Text, out float newValue))
                {
                    // Clamp the value to the 0-1 range for Alpha, 0-10 for Power
                    if (textBox.Name.EndsWith("AlphaTextBox"))
                    {
                        newValue = Math.Max(0f, Math.Min(1f, newValue));
                    }
                    else if (textBox.Name.EndsWith("PowerTextBox"))
                    {
                        newValue = Math.Max(0f, Math.Min(10f, newValue));
                    }

                    float selectedTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];

                    XElement elementToUpdate = null;
                    if (textBox.Name.EndsWith("AlphaTextBox"))
                    {
                        elementToUpdate = dataWrapper.Ds1rAElement;
                    }
                    else if (textBox.Name.EndsWith("PowerTextBox"))
                    {
                        elementToUpdate = dataWrapper.Ds1rPElement;
                    }

                    if (elementToUpdate != null)
                    {
                        UpdateXmlDocument(elementToUpdate, newValue, selectedTime);
                        UpdateUiForCurrentTime(parentGroupBox, selectedTime);
                    }
                }
                else
                {
                    MessageBox.Show("Invalid number format. Please enter a decimal number.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    float selectedTime = dataWrapper.AllUniqueTimes[dataWrapper.CurrentTimeIndex];
                    UpdateUiForCurrentTime(parentGroupBox, selectedTime); // Revert to the last valid value
                }
            }
        }

        // Generic method to update an XML element based on its type (ConstFloat or FloatSequence)
        private void UpdateXmlDocument(XElement element, float newValue, float time)
        {
            if (element == null) return;

            // Check if the element is a FloatSequence
            if (element.Attribute(_xsi + "type")?.Value == "FloatSequence")
            {
                var tick = element.Descendants("FloatTick").FirstOrDefault(t => (float?)t.Attribute("Time") == time);
                if (tick != null)
                {
                    tick.SetAttributeValue("Value", newValue.ToString("F4"));
                }
            }
            else // Assume ConstFloat
            {
                element.SetAttributeValue("Value", newValue.ToString("F4"));
            }
        }

        // Specific update method for ColorSequenceNode, which has a different structure
        private void UpdateXmlDocumentForColorSequenceNode(XElement node, float newR, float newG, float newB, float time)
        {
            var tick = node.Descendants("ColorTick").FirstOrDefault(t => (float?)t.Attribute("Time") == time);
            if (tick != null)
            {
                tick.SetAttributeValue("R", newR.ToString("F4"));
                tick.SetAttributeValue("G", newG.ToString("F4"));
                tick.SetAttributeValue("B", newB.ToString("F4"));
            }
        }

        // Saves the modified XML document to a new file.
        private void saveAsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "XML Files (*.xml)|*.xml|All Files (*.*)|*.*";
            saveFileDialog.Title = "Save the modified XML file";
            saveFileDialog.FileName = Path.GetFileName(_filePath);

            if (saveFileDialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    // Update the EffectID in the XML before saving, if it exists
                    if (_effectIdElement != null)
                    {
                        _effectIdElement.Value = this.effectIdTextBox.Text;
                    }

                    // Save the modified document to the new path.
                    _xmlDocument.Save(saveFileDialog.FileName);
                    MessageBox.Show("File saved successfully!", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"An error occurred while saving the file: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        // Helper method to create the initial Base Color controls.
        private void SetupBaseColorControls(GroupBox groupBox, ColorDataWrapper dataWrapper, Point startLocation)
        {
            var defaultFont = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Regular);

            // Color Label
            Label colorLabel = new Label();
            colorLabel.Text = "Base Color";
            colorLabel.Font = new Font(colorLabel.Font, FontStyle.Bold);
            colorLabel.Location = startLocation;
            groupBox.Controls.Add(colorLabel);

            // Define consistent offsets for components
            int labelXOffset = startLocation.X;
            int textboxXOffset = startLocation.X + 25;
            int componentYOffset = startLocation.Y + 30; // Vertical spacing

            // Add the R, G, B labels and textboxes
            string[] components = { "R", "G", "B" };
            int yOffset = 0;
            foreach (var component in components)
            {
                Label label = new Label();
                label.Text = $"{component}:";
                label.Font = defaultFont;
                label.Location = new Point(labelXOffset, componentYOffset + yOffset);
                label.AutoSize = true;
                groupBox.Controls.Add(label);

                TextBox textBox = new TextBox();
                textBox.Location = new Point(textboxXOffset, componentYOffset + yOffset - 3); // Adjust for alignment
                textBox.Width = 80;
                textBox.Name = $"base{component}TextBox";
                textBox.Font = defaultFont;

                // Add the Leave event handler
                textBox.Tag = groupBox;
                textBox.Leave += new EventHandler(BaseColorTextBox_Leave);

                groupBox.Controls.Add(textBox);
                yOffset += 25; // Increase the vertical spacing
            }

            // Color Preview Panel
            Panel colorPreviewPanel = new Panel();
            int panelHeight = (2 * 25) + 23;
            colorPreviewPanel.Size = new Size(panelHeight, panelHeight); // Make width equal to height
            colorPreviewPanel.Location = new Point(startLocation.X + 110, componentYOffset - 3); // Top aligns with R textbox
            colorPreviewPanel.BorderStyle = BorderStyle.FixedSingle;
            colorPreviewPanel.Name = $"baseColorPanel";
            colorPreviewPanel.Click += new EventHandler(ColorPreviewPanel_Click);
            colorPreviewPanel.Tag = groupBox;
            groupBox.Controls.Add(colorPreviewPanel);
        }

        // New helper method to create the Tint Color controls.
        private void SetupTintColorControls(GroupBox groupBox, ColorDataWrapper dataWrapper, Point startLocation)
        {
            var defaultFont = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Regular);

            Label colorLabel = new Label();
            colorLabel.Text = "Color Tint";
            colorLabel.Font = new Font(colorLabel.Font, FontStyle.Bold);
            colorLabel.Location = startLocation;
            groupBox.Controls.Add(colorLabel);

            int labelXOffset = startLocation.X;
            int textboxXOffset = startLocation.X + 25;
            int componentYOffset = startLocation.Y + 30;

            string[] components = { "R", "G", "B" };
            int yOffset = 0;
            foreach (var component in components)
            {
                Label label = new Label();
                label.Text = $"{component}:";
                label.Font = defaultFont;
                label.Location = new Point(labelXOffset, componentYOffset + yOffset);
                label.AutoSize = true;
                groupBox.Controls.Add(label);

                TextBox textBox = new TextBox();
                textBox.Location = new Point(textboxXOffset, componentYOffset + yOffset - 3);
                textBox.Width = 80;
                textBox.Name = $"tint{component}TextBox";
                textBox.Font = defaultFont;
                textBox.Tag = groupBox;
                textBox.Leave += new EventHandler(TintColorTextBox_Leave);
                groupBox.Controls.Add(textBox);
                yOffset += 25;
            }

            Panel colorPreviewPanel = new Panel();
            int panelHeight = (2 * 25) + 23;
            colorPreviewPanel.Size = new Size(panelHeight, panelHeight);
            colorPreviewPanel.Location = new Point(startLocation.X + 110, componentYOffset - 3);
            colorPreviewPanel.BorderStyle = BorderStyle.FixedSingle;
            colorPreviewPanel.Name = $"tintColorPanel";
            colorPreviewPanel.Click += new EventHandler(ColorPreviewPanel_Click);
            colorPreviewPanel.Tag = groupBox;
            groupBox.Controls.Add(colorPreviewPanel);
        }

        // New helper method to set up DS1RData and Blend Mode controls together
        private void SetupDs1rDataAndBlendModeControls(GroupBox groupBox, ColorDataWrapper dataWrapper, XElement mainElement, Point startLocation)
        {
            var defaultFont = new Font(groupBox.Font.FontFamily, groupBox.Font.Size, FontStyle.Regular);

            int labelXOffset = startLocation.X;
            // Increased the offset to prevent overlap
            int textboxXOffset = startLocation.X + 80;
            int verticalSpacing = 25;

            // Add TextBoxes for Alpha
            Label aLabel = new Label();
            aLabel.Text = "Alpha:";
            aLabel.Font = defaultFont;
            aLabel.Location = new Point(labelXOffset, startLocation.Y + 30);
            aLabel.AutoSize = true;
            groupBox.Controls.Add(aLabel);

            TextBox alphaTextBox = new TextBox();
            alphaTextBox.Location = new Point(textboxXOffset, startLocation.Y + 30 - 3);
            alphaTextBox.Name = "Ds1rAlphaTextBox";
            alphaTextBox.Width = 80;
            alphaTextBox.Font = defaultFont;
            alphaTextBox.Tag = groupBox;
            alphaTextBox.Leave += new EventHandler(Ds1rTextBox_Leave);
            groupBox.Controls.Add(alphaTextBox);

            // Add TextBoxes for Power
            Label pLabel = new Label();
            pLabel.Text = "Power:";
            pLabel.Font = defaultFont;
            pLabel.Location = new Point(labelXOffset, startLocation.Y + 30 + verticalSpacing);
            pLabel.AutoSize = true;
            groupBox.Controls.Add(pLabel);

            TextBox powerTextBox = new TextBox();
            powerTextBox.Location = new Point(textboxXOffset, startLocation.Y + 30 + verticalSpacing - 3);
            powerTextBox.Name = "Ds1rPowerTextBox";
            powerTextBox.Width = 80;
            powerTextBox.Font = defaultFont;
            powerTextBox.Tag = groupBox;
            powerTextBox.Leave += new EventHandler(Ds1rTextBox_Leave);
            groupBox.Controls.Add(powerTextBox);

            // Add Blend Mode label and ComboBox
            Label blendModeLabel = new Label();
            blendModeLabel.Text = "Blend Mode:";
            blendModeLabel.Font = defaultFont;
            blendModeLabel.Location = new Point(labelXOffset, startLocation.Y + 30 + verticalSpacing * 2);
            blendModeLabel.AutoSize = true;
            groupBox.Controls.Add(blendModeLabel);

            ComboBox blendModeComboBox = new ComboBox();
            blendModeComboBox.Location = new Point(textboxXOffset, startLocation.Y + 30 + verticalSpacing * 2 - 3);
            blendModeComboBox.Width = 80;
            blendModeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            blendModeComboBox.Items.AddRange(new string[] { "Add", "Subtract" });
            blendModeComboBox.Font = defaultFont;
            blendModeComboBox.Tag = groupBox;
            blendModeComboBox.SelectedIndexChanged += new EventHandler(BlendModeComboBox_SelectedIndexChanged);
            groupBox.Controls.Add(blendModeComboBox);

            // Find the Unk elements to determine the initial blend mode
            XElement unk9Element = mainElement.Element("Unk9");
            XElement unk16Element = mainElement.Element("Unk16");
            XElement unk17Element = mainElement.Element("Unk17");

            // Store these references in the data wrapper
            if (dataWrapper != null)
            {
                dataWrapper.Unk9Element = unk9Element;
                dataWrapper.Unk16Element = unk16Element;
                dataWrapper.Unk17Element = unk17Element;
            }

            // Read values and set the default selection
            if (unk16Element != null && unk17Element != null)
            {
                if (int.TryParse(unk16Element.Value, out int unk16) &&
                    int.TryParse(unk17Element.Value, out int unk17))
                {
                    if (unk16 == -1 && unk17 == -1)
                    {
                        blendModeComboBox.SelectedItem = "Add";
                    }
                    else if (unk16 == -2 && unk17 == -2)
                    {
                        blendModeComboBox.SelectedItem = "Subtract";
                    }
                }
            }
        }
    }
}
