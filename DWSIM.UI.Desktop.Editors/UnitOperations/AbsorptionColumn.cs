﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using DWSIM.Interfaces;
using DWSIM.Interfaces.Enums.GraphicObjects;
using DWSIM.UnitOperations.UnitOperations;
using DWSIM.UnitOperations.Reactors;
using DWSIM.UnitOperations.SpecialOps;
using DWSIM.UnitOperations.Streams;
using DWSIM.Thermodynamics.Streams;

using Eto.Forms;

using cv = DWSIM.SharedClasses.SystemsOfUnits.Converter;
using s = DWSIM.UI.Shared.Common;
using Eto.Drawing;

using StringResources = DWSIM.UI.Desktop.Shared.StringArrays;
using DWSIM.Thermodynamics.PropertyPackages;
using DWSIM.Interfaces.Enums;
using DWSIM.UnitOperations.UnitOperations.Auxiliary.SepOps;

using DWSIM.ExtensionMethods;

namespace DWSIM.UI.Desktop.Editors
{
    public class AbsorptionColumnEditor
    {

        public AbsorptionColumn column;

        public DynamicLayout container;

        public AbsorptionColumnEditor(ISimulationObject selectedobject, DynamicLayout layout)
        {
            column = (AbsorptionColumn)selectedobject;
            container = layout;
            Initialize();
        }
        void CallSolverIfNeeded()
        {
            if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke();
        }

        void Initialize()
        {

            var su = column.GetFlowsheet().FlowsheetOptions.SelectedUnitSystem;
            var nf = column.GetFlowsheet().FlowsheetOptions.NumberFormat;
            var nff = column.GetFlowsheet().FlowsheetOptions.FractionNumberFormat;

            s.CreateAndAddLabelRow(container, "Object Details");

            s.CreateAndAddTwoLabelsRow(container, "Type", column.GetDisplayName());

            s.CreateAndAddTwoLabelsRow(container, "Status", column.GraphicObject.Active ? "Active" : "Inactive");

            s.CreateAndAddStringEditorRow(container, "Name", column.GraphicObject.Tag, (TextBox arg3, EventArgs ev) =>
            {
                column.GraphicObject.Tag = arg3.Text;
            }, () => CallSolverIfNeeded());

            s.CreateAndAddLabelRow(container, "Property Package");

            var proppacks = column.GetFlowsheet().PropertyPackages.Values.Select((x) => x.Tag).ToList();

            if (proppacks.Count == 0)
            {
                column.GetFlowsheet().ShowMessage("Error: please add at least one Property Package before continuing.", IFlowsheet.MessageType.GeneralError);
            }
            else
            {
                var pp = column.PropertyPackage;
                string selectedpp = "";
                if (pp != null) selectedpp = pp.Tag;
                s.CreateAndAddDropDownRow(container, "Property Package", proppacks, proppacks.IndexOf(selectedpp), (DropDown arg1, EventArgs ev) =>
                {
                    column.PropertyPackage = (IPropertyPackage)column.GetFlowsheet().PropertyPackages.Values.Where((x) => x.Tag == proppacks[arg1.SelectedIndex]).FirstOrDefault();
                }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });
            }


            var flashalgos = column.GetFlowsheet().FlowsheetOptions.FlashAlgorithms.Select(x => x.Tag).ToList();
            flashalgos.Insert(0, "Default");

            var cbFlashAlg = s.CreateAndAddDropDownRow(container, "Flash Algorithm", flashalgos, 0, null);

            if (!string.IsNullOrEmpty(column.PreferredFlashAlgorithmTag))
                cbFlashAlg.SelectedIndex = Array.IndexOf(flashalgos.ToArray(), column.PreferredFlashAlgorithmTag);
            else
                cbFlashAlg.SelectedIndex = 0;

            cbFlashAlg.SelectedIndexChanged += (sender, e) =>
            {
                column.PreferredFlashAlgorithmTag = cbFlashAlg.SelectedValue.ToString();
                if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke();
            };

            s.CreateAndAddLabelRow(container, "Object Properties");

            s.CreateAndAddDropDownRow(container, "Operating Mode", new List<string> {"Gas-Liquid Absorption", "Liquid-Liquid Extraction"}, (int)column.OperationMode, (arg1, e) => {
                switch (arg1.SelectedIndex)
                {
                    case 0:
                        column.OperationMode = AbsorptionColumn.OpMode.Absorber;
                        break;
                    case 1:
                        column.OperationMode = AbsorptionColumn.OpMode.Extractor;
                        break;
                }

            });

            s.CreateAndAddButtonRow(container, "Define Number of Stages", null, (arg1, e) =>
            {

                var np = new Eto.Forms.NumericStepper();
                np.MinValue = 3;
                np.MaxValue = 100;
                np.MaximumDecimalPlaces = 0;
                np.Value = column.NumberOfStages;
                np.ValueChanged += (sender, e2) =>
                {
                    var refval = (int)np.Value;
                    column.NumberOfStages = refval;
                    int ne, nep, dif, i;
                    ne = refval;
                    nep = column.Stages.Count;
                    dif = ne - nep;
                    if (dif != 0)
                    {
                        if (dif < 0)
                        {
                            column.Stages.RemoveRange(ne - 1, -dif);
                        }
                        else if (dif > 0)
                        {
                            for (i = 0; i <= dif; i++)
                            {
                                column.Stages.Insert(column.Stages.Count - 1, new Stage(Guid.NewGuid().ToString()) { P = 101325, Efficiency = 1.0f });
                                column.Stages[column.Stages.Count - 2].Name = "Stage " + (column.Stages.Count - 2).ToString();
                            }
                        }
                    }
                };

                s.CreateDialog(np, "Set Number of Stages").ShowModal(container);

            });

            s.CreateAndAddButtonRow(container, "Edit Stages", null, (arg1, e) =>
            {

                var sview = DWSIM.UI.Shared.Common.GetDefaultContainer();

                s.CreateAndAddLabelRow(sview, "Edit Stages");
                s.CreateAndAddLabelRow(sview, "Number / Name / Pressure");
                var tlist = new List<TextBox>();
                foreach (var stage in column.Stages)
                {
                    tlist.Add(s.CreateAndAddDoubleTextBoxRow(sview, nf, (column.Stages.IndexOf(stage) + 1).ToString(), stage.Name, cv.ConvertFromSI(su.pressure, stage.P),
                                                   (arg10, arg20) =>
                                                   {
                                                       stage.Name = arg10.Text;
                                                   }, (arg11, arg22) =>
                                                   {
                                                       if (s.IsValidDouble(arg11.Text))
                                                       {
                                                           stage.P = cv.ConvertToSI(su.pressure, Double.Parse(arg11.Text));
                                                       }
                                                   }));
                }
                s.CreateAndAddLabelAndButtonRow(sview, "Interpolate Pressures", "Interpolate", null, (sender2, e2) =>
                {
                    var first = tlist[0].Text.ToDoubleFromCurrent();
                    var last = tlist[tlist.Count - 1].Text.ToDoubleFromCurrent();
                    var n = tlist.Count;
                    int i = 1;
                    for (i = 1; i < n - 1; i++)
                    {
                        tlist[i].Text = (first + (last - first) * i / (n - 1)).ToString(nf);
                    }
                });
                s.CreateAndAddDescriptionRow(sview, "Calculate inner pressures using end stage defined values.");

                var scroll = new Eto.Forms.Scrollable();
                scroll.Content = sview;

                s.CreateDialog(scroll, "Edit Stages", 400, 600).ShowModal(container);

            });

            var istrs = column.GraphicObject.InputConnectors.Where((x) => x.IsAttached && x.ConnectorName.Contains("Feed")).Select((x2) => x2.AttachedConnector.AttachedFrom.Name).ToList();
            var ostrs = column.GraphicObject.OutputConnectors.Where((x) => x.IsAttached && x.ConnectorName.Contains("Side")).Select((x2) => x2.AttachedConnector.AttachedTo.Name).ToList();
            var dist = column.GraphicObject.OutputConnectors.Where((x) => x.IsAttached && x.ConnectorName.Contains("Top Product")).Select((x2) => x2.AttachedConnector.AttachedTo.Name).ToList();
            var bottoms = column.GraphicObject.OutputConnectors.Where((x) => x.IsAttached && x.ConnectorName.Contains("Bottoms Product")).Select((x2) => x2.AttachedConnector.AttachedTo.Name).ToList();
         
            foreach (var id in istrs)
            {
                if (column.MaterialStreams.Values.Where(x => x.StreamID == id).Count() == 0)
                {
                    column.MaterialStreams.Add(id, new StreamInformation()
                    {
                        StreamID = id,
                        ID = id,
                        StreamType = StreamInformation.Type.Material,
                        StreamBehavior = StreamInformation.Behavior.Feed
                    });
                }
            }
            foreach (var id in ostrs)
            {
                if (column.MaterialStreams.Values.Where(x => x.StreamID == id).Count() == 0)
                {
                    column.MaterialStreams.Add(id, new StreamInformation()
                    {
                        StreamID = id,
                        ID = id,
                        StreamType = StreamInformation.Type.Material,
                        StreamBehavior = StreamInformation.Behavior.Sidedraw
                    });
                }
            }
            foreach (var id in dist)
            {
                if (column.MaterialStreams.Values.Where(x => x.StreamID == id).Count() == 0)
                {
                    column.MaterialStreams.Add(id, new StreamInformation()
                    {
                        StreamID = id,
                        ID = id,
                        StreamType = StreamInformation.Type.Material,
                        StreamBehavior = StreamInformation.Behavior.Distillate
                    });
                }
            }
            foreach (var id in bottoms)
            {
                if (column.MaterialStreams.Values.Where(x => x.StreamID == id).Count() == 0)
                {
                    column.MaterialStreams.Add(id, new StreamInformation()
                    {
                        StreamID = id,
                        ID = id,
                        StreamType = StreamInformation.Type.Material,
                        StreamBehavior = StreamInformation.Behavior.BottomsLiquid
                    });
                }
            }
            List<string> remove = new List<string>();
            foreach (var si in column.MaterialStreams.Values)
            {
                if (!istrs.Contains(si.StreamID) && !ostrs.Contains(si.StreamID) && !dist.Contains(si.StreamID) && !bottoms.Contains(si.StreamID)) { remove.Add(si.ID); }
                if (!column.GetFlowsheet().SimulationObjects.ContainsKey(si.StreamID)) { remove.Add(si.ID); }
            }
            foreach (var id in remove)
            {
                if (column.MaterialStreams.ContainsKey(id)) { column.MaterialStreams.Remove(id); }
            }

            var stageNames = column.Stages.Select((x) => x.Name).ToList();
            stageNames.Insert(0, "");
            var stageIDs = column.Stages.Select((x) => x.ID).ToList();
            stageIDs.Insert(0, "");

            s.CreateAndAddLabelRow(container, "Streams");

            foreach (var si in column.MaterialStreams.Values)
            {
                if (si.StreamBehavior == StreamInformation.Behavior.Feed)
                {
                    s.CreateAndAddDropDownRow(container, "[FEED] " + column.GetFlowsheet().SimulationObjects[si.StreamID].GraphicObject.Tag,
                                         stageNames, stageIDs.IndexOf(si.AssociatedStage), (arg1, arg2) =>
                                         {
                                             si.AssociatedStage = stageIDs[arg1.SelectedIndex];
                                         });
                }
                else if (si.StreamBehavior == StreamInformation.Behavior.Sidedraw)
                {
                    s.CreateAndAddDropDownRow(container, "[SIDEDRAW] " + column.GetFlowsheet().SimulationObjects[si.StreamID].GraphicObject.Tag,
                                         stageNames, stageIDs.IndexOf(si.AssociatedStage), (arg1, arg2) =>
                                         {
                                             si.AssociatedStage = stageIDs[arg1.SelectedIndex];
                                         });
                }
                else if (si.StreamBehavior == StreamInformation.Behavior.Distillate || si.StreamBehavior == StreamInformation.Behavior.OverheadVapor)
                {
                    s.CreateAndAddDropDownRow(container, "[TOP PRODUCT] " + column.GetFlowsheet().SimulationObjects[si.StreamID].GraphicObject.Tag,
                                         stageNames, stageIDs.IndexOf(si.AssociatedStage), (arg1, arg2) =>
                                         {
                                             si.AssociatedStage = stageIDs[arg1.SelectedIndex];
                                         });
                }
                else if (si.StreamBehavior == StreamInformation.Behavior.BottomsLiquid)
                {
                    s.CreateAndAddDropDownRow(container, "[BOTTOMS PRODUCT] " + column.GetFlowsheet().SimulationObjects[si.StreamID].GraphicObject.Tag,
                                         stageNames, stageIDs.IndexOf(si.AssociatedStage), (arg1, arg2) =>
                                         {
                                             si.AssociatedStage = stageIDs[arg1.SelectedIndex];
                                         });
                }
            }
           
            s.CreateAndAddLabelRow(container, "Side Draw Specs");
            var sdphases = new List<string>() { "L", "V" };
            foreach (var si in column.MaterialStreams.Values)
            {
                string sp = "L";
                switch (si.StreamPhase)
                {
                    case StreamInformation.Phase.L:
                        sp = "L";
                        break;
                    case StreamInformation.Phase.V:
                        sp = "V";
                        break;
                }
                if (si.StreamBehavior == StreamInformation.Behavior.Sidedraw)
                {
                    s.CreateAndAddDropDownRow(container, column.GetFlowsheet().SimulationObjects[si.StreamID].GraphicObject.Tag + " / Draw Phase",
                                             sdphases, sdphases.IndexOf(sp), (arg1, arg2) =>
                                             {
                                                 switch (arg1.SelectedIndex)
                                                 {
                                                     case 0:
                                                         si.StreamPhase = StreamInformation.Phase.L;
                                                         break;
                                                     case 1:
                                                         si.StreamPhase = StreamInformation.Phase.V;
                                                         break;
                                                 }
                                             });
                    s.CreateAndAddTextBoxRow(container, nf, column.GetFlowsheet().SimulationObjects[si.StreamID].GraphicObject.Tag + " / Molar Flow (" + su.molarflow + ")",
                                             cv.ConvertFromSI(su.molarflow, si.FlowRate.Value), (arg1, arg2) =>
                                             {
                                                 if (s.IsValidDouble(arg1.Text))
                                                 {
                                                     si.FlowRate.Value = cv.ConvertToSI(su.molarflow, Double.Parse(arg1.Text));
                                                 }
                                             });
                }
            }


            s.CreateAndAddLabelRow(container, "Column Solver Selection");

            var methods = new string[] { "Wang-Henke (Bubble Point)", "Naphtali-Sandholm (Newton)", "Russell (Inside-Out)", "Burningham-Otto (Sum Rates) (Absorber Only)" };
            var strategies = new string[] { "Ideal K first, then Rigorous", "Ideal H first, then Rigorous", "Ideal K+H first, then Rigorous", "Direct Rigorous" };

            s.CreateAndAddDropDownRow(container, "Solving Method", methods.ToList(), (int)column.SolvingMethod, (sender, e) =>
            {
                column.SolvingMethod = sender.SelectedIndex;
            });

            s.CreateAndAddDropDownRow(container, "Solving Scheme", strategies.ToList(), (int)column.SolverScheme, (sender, e) =>
            {
                column.SolverScheme = (UnitOperations.UnitOperations.Column.SolvingScheme)sender.SelectedIndex;
            });

            s.CreateAndAddTextBoxRow(container, "N0", "Maximum Iterations", column.MaxIterations,
            (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.MaxIterations = (int)sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddTextBoxRow(container, nf, "Convergence Tolerance (External Loop)", column.ExternalLoopTolerance,
            (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.ExternalLoopTolerance = sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddTextBoxRow(container, nf, "Convergence Tolerance (Internal Loop)", column.InternalLoopTolerance,
            (sender, e) =>
            {
            if (sender.Text.IsValidDouble()) column.InternalLoopTolerance = sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });
            
            s.CreateAndAddTextBoxRow(container, nf, "Maximum Temperature Change Step (" + su.deltaT + ")", cv.ConvertFromSI(su.deltaT, column.MaximumTemperatureStep),
            (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.MaximumTemperatureStep = cv.ConvertToSI(su.deltaT, sender.Text.ToDoubleFromCurrent());
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddLabelRow(container, "Bubble Point Solver Settings");

            s.CreateAndAddTextBoxRow(container, "N0", "Stop at iteration number", column.StopAtIterationNumber,
                (sender, e) =>
                {
                    if (sender.Text.IsValidDouble()) column.StopAtIterationNumber = (int)sender.Text.ToDoubleFromCurrent();
                }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddLabelRow(container, "Newton Solver Settings");

            var solvers = new List<string>() { "Limited Memory BGFS", "Truncated Newton", "Simplex", "IPOPT", "Particle Swarm", "Local Unimodal Sampling", "Gradient Descent", "Differential Evolution", "Particle Swarm Optimization", "Many Optimizing Liaisons", "Mesh" };

            s.CreateAndAddDropDownRow(container, "Non-Linear Solver", solvers, (int)column.NS_Solver, (sender, e) => column.NS_Solver = (DWSIM.Interfaces.Enums.OptimizationMethod)sender.SelectedIndex);

            s.CreateAndAddTextBoxRow(container, nf, "Iteration Variables: Lower Bound", column.NS_LowerBound, (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.NS_LowerBound = (int)sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddTextBoxRow(container, nf, "Iteration Variables: Upper Bound", column.NS_UpperBound, (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.NS_UpperBound = (int)sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddTextBoxRow(container, nf, "Iteration Variables: Derivative Perturbation", column.SC_NumericalDerivativeStep, (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.SC_NumericalDerivativeStep = (int)sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddCheckBoxRow(container, "Iteration Variables: Simplex Preconditioning", column.NS_SimplexPreconditioning, (sender, e) => column.NS_SimplexPreconditioning = sender.Checked.GetValueOrDefault());

            s.CreateAndAddLabelRow(container, "Inside-Out Solver Settings");

            s.CreateAndAddDropDownRow(container, "Non-Linear Solver", solvers, (int)column.IO_Solver, (sender, e) => column.IO_Solver = (DWSIM.Interfaces.Enums.OptimizationMethod)sender.SelectedIndex);

            s.CreateAndAddTextBoxRow(container, nf, "Iteration Variables: Lower Bound", column.IO_LowerBound, (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.IO_LowerBound = (int)sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddTextBoxRow(container, nf, "Iteration Variables: Upper Bound", column.IO_UpperBound, (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.IO_UpperBound = (int)sender.Text.ToDoubleFromCurrent();
            }, () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddTextBoxRow(container, nf, "Iteration Variables: Derivative Perturbation", column.IO_NumericalDerivativeStep, (sender, e) =>
            {
                if (sender.Text.IsValidDouble()) column.IO_NumericalDerivativeStep = (int)sender.Text.ToDoubleFromCurrent();
            });

            s.CreateAndAddCheckBoxRow(container, "Adjust Sb Scaling Factor", column.AdjustSb, (sender, e) => column.AdjustSb = sender.Checked.GetValueOrDefault(), () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddCheckBoxRow(container, "Calculate Kb by Weighted Average", column.KbjWeightedAverage, (sender, e) => column.KbjWeightedAverage = sender.Checked.GetValueOrDefault(), () => { if (GlobalSettings.Settings.CallSolverOnEditorPropertyChanged) ((Shared.Flowsheet)column.GetFlowsheet()).HighLevelSolve.Invoke(); });

            s.CreateAndAddEmptySpace(container);
            s.CreateAndAddEmptySpace(container);
            s.CreateAndAddEmptySpace(container);

        }


    }

}

