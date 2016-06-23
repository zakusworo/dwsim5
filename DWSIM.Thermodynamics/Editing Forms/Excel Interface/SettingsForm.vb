﻿Imports System.IO
Imports Cudafy
Imports Cudafy.Host

Public Class SettingsForm

    Private loaded As Boolean = False

    Private flashsettings As Dictionary(Of Interfaces.Enums.FlashSetting, String)

    Private Sub SettingsForm_FormClosing(sender As Object, e As FormClosingEventArgs) Handles Me.FormClosing

        Dim inifile As String = My.Computer.FileSystem.SpecialDirectories.MyDocuments & Path.DirectorySeparatorChar & "DWSIM Application Data" & Path.DirectorySeparatorChar & "config.ini"

        Try
            GlobalSettings.Settings.SaveExcelSettings(inifile)
        Catch ex As Exception
            MessageBox.Show("Error saving current settings." & vbCrLf & "Error message: " & ex.ToString, "Error reading settings", MessageBoxButtons.OK)
        End Try

    End Sub

    Private Sub SettingsForm_Load(sender As Object, e As EventArgs) Handles MyBase.Load

        Application.EnableVisualStyles()

        My.Application.ChangeCulture("en")
        My.Application.ChangeUICulture("en")

        Dim inifile As String = My.Computer.FileSystem.SpecialDirectories.MyDocuments & Path.DirectorySeparatorChar & "DWSIM Application Data" & Path.DirectorySeparatorChar & "config.ini"

        tbConfigFilePath.Text = inifile

        Try
            GlobalSettings.Settings.LoadExcelSettings(inifile)
        Catch ex As Exception
            MessageBox.Show("Error reading config file. Default settings will be used instead." & vbCrLf & "Error message: " & ex.ToString, "Error reading settings", MessageBoxButtons.OK)
        End Try

        Me.cbErrorHandlingMode.SelectedIndex = GlobalSettings.Settings.ExcelErrorHandlingMode

        Dim i As Integer = 0
        Me.cbParallelism.Items.Clear()
        Me.cbParallelism.Items.Add("Default")
        For i = 1 To System.Environment.ProcessorCount
            Me.cbParallelism.Items.Add(i.ToString)
        Next
        If GlobalSettings.Settings.MaxDegreeOfParallelism = -1 Then
            Me.cbParallelism.SelectedIndex = 0
        ElseIf GlobalSettings.Settings.MaxDegreeOfParallelism <= System.Environment.ProcessorCount Then
            Me.cbParallelism.SelectedItem = GlobalSettings.Settings.MaxDegreeOfParallelism.ToString
        Else
            Me.cbParallelism.SelectedIndex = Me.cbParallelism.Items.Count - 1
        End If

        Me.chkEnableParallelCalcs.Checked = GlobalSettings.Settings.EnableParallelProcessing
        Me.chkEnableGPUProcessing.Checked = GlobalSettings.Settings.EnableGPUProcessing
        Me.cbGPU.Enabled = Me.chkEnableGPUProcessing.Checked
        Me.tbGPUCaps.Enabled = Me.chkEnableGPUProcessing.Checked
        Me.cbParallelism.Enabled = Me.chkEnableParallelCalcs.Checked
        Me.chkEnableSIMD.Checked = GlobalSettings.Settings.UseSIMDExtensions

        Me.cbGPU.Items.Clear()

        Task.Factory.StartNew(Function()
                                  Dim list As New List(Of String)
                                  Try
                                      CudafyModes.Target = eGPUType.Cuda
                                      For Each prop As GPGPUProperties In CudafyHost.GetDeviceProperties(CudafyModes.Target, False)
                                          list.Add("CUDA | " & prop.Name & " (" & prop.DeviceId & ")")
                                      Next
                                  Catch ex As Exception

                                  End Try
                                  CudafyModes.Target = eGPUType.OpenCL
                                  For Each prop As GPGPUProperties In CudafyHost.GetDeviceProperties(CudafyModes.Target, False)
                                      list.Add("OpenCL | " & prop.Name & " (" & prop.DeviceId & ")")
                                  Next
                                  Return list
                              End Function).ContinueWith(Sub(t)
                                                             Me.chkEnableGPUProcessing.Enabled = t.Result.Count > 0
                                                             Me.cbGPU.Items.AddRange(t.Result.ToArray)
                                                             CudafyModes.Target = GlobalSettings.Settings.CudafyTarget
                                                             If GlobalSettings.Settings.SelectedGPU <> "" Then
                                                                 For Each s As String In Me.cbGPU.Items
                                                                     If s = GlobalSettings.Settings.SelectedGPU Then
                                                                         Me.cbGPU.SelectedItem = s
                                                                         Exit For
                                                                     End If
                                                                 Next
                                                             Else
                                                                 'If Me.cbGPU.Items.Count > 0 Then Me.cbGPU.SelectedIndex = 0
                                                             End If
                                                             loaded = True
                                                         End Sub, TaskScheduler.FromCurrentSynchronizationContext)

    End Sub

    Private Sub chkEnableParallelCalcs_CheckedChanged(sender As Object, e As EventArgs) Handles chkEnableParallelCalcs.CheckedChanged
        Me.cbParallelism.Enabled = Me.chkEnableParallelCalcs.Checked
        GlobalSettings.Settings.EnableParallelProcessing = Me.chkEnableParallelCalcs.Checked
    End Sub
    Private Sub cbParallelism_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cbParallelism.SelectedIndexChanged
        If Me.cbParallelism.SelectedIndex = 0 Then
            GlobalSettings.Settings.MaxDegreeOfParallelism = -1
        Else
            GlobalSettings.Settings.MaxDegreeOfParallelism = Me.cbParallelism.SelectedItem
        End If
        Settings.MaxDegreeOfParallelism = GlobalSettings.Settings.MaxDegreeOfParallelism
    End Sub

    Private Sub chkEnableSIMD_CheckedChanged(sender As Object, e As EventArgs) Handles chkEnableSIMD.CheckedChanged
        GlobalSettings.Settings.UseSIMDExtensions = chkEnableSIMD.Checked
        Settings.UseSIMDExtensions = GlobalSettings.Settings.UseSIMDExtensions
    End Sub

    Private Sub cbGPU_SelectedIndexChanged(sender As System.Object, e As System.EventArgs) Handles cbGPU.SelectedIndexChanged
        If Not cbGPU.SelectedItem Is Nothing Then
            If cbGPU.SelectedItem.ToString.Contains("Emulator") Then
                GlobalSettings.Settings.CudafyTarget = eGPUType.Emulator
            ElseIf cbGPU.SelectedItem.ToString.Contains("CUDA") Then
                GlobalSettings.Settings.CudafyTarget = eGPUType.Cuda
            Else
                GlobalSettings.Settings.CudafyTarget = eGPUType.OpenCL
            End If
            Settings.CudafyTarget = GlobalSettings.Settings.CudafyTarget
            Try
                For Each prop As GPGPUProperties In CudafyHost.GetDeviceProperties(GlobalSettings.Settings.CudafyTarget, False)
                    If Me.cbGPU.SelectedItem.ToString.Split("|")(1).Contains(prop.Name) Then
                        GlobalSettings.Settings.SelectedGPU = Me.cbGPU.SelectedItem.ToString
                        GlobalSettings.Settings.CudafyDeviceID = prop.DeviceId
                        Settings.SelectedGPU = Me.cbGPU.SelectedItem.ToString
                        Settings.CudafyDeviceID = GlobalSettings.Settings.CudafyDeviceID
                        GetCUDACaps(prop)
                        Exit For
                    End If
                Next
            Catch ex As Exception

            End Try
            If loaded Then
                If Not Settings.gpu Is Nothing Then
                    Settings.gpu.Dispose()
                    Settings.gpu = Nothing
                Else
                    Calculator.InitComputeDevice()
                End If
            End If
        End If
    End Sub

    Private Sub chkEnableGPUProcessing_CheckedChanged(sender As System.Object, e As System.EventArgs) Handles chkEnableGPUProcessing.CheckedChanged
        Me.cbGPU.Enabled = chkEnableGPUProcessing.Checked
        Me.tbGPUCaps.Enabled = chkEnableGPUProcessing.Checked
        GlobalSettings.Settings.EnableGPUProcessing = chkEnableGPUProcessing.Checked
        If loaded And chkEnableGPUProcessing.Checked Then cbGPU_SelectedIndexChanged(sender, e)
    End Sub

    Public Sub GetCUDACaps(prop As GPGPUProperties)

        Dim i As Integer = 0

        Me.tbGPUCaps.Text = ""

        Me.tbGPUCaps.AppendText(String.Format("   --- General Information for device {0} ---", i) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Name:  {0}", prop.Name) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Device Id:  {0}", prop.DeviceId) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Compute capability:  {0}.{1}", prop.Capability.Major, prop.Capability.Minor) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Clock rate: {0}", prop.ClockRate) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Simulated: {0}", prop.IsSimulated) & vbCrLf)

        Me.tbGPUCaps.AppendText(String.Format("   --- Memory Information for device {0} ---", i) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Total global mem:  {0}", prop.TotalMemory) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Total constant Mem:  {0}", prop.TotalConstantMemory) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Max mem pitch:  {0}", prop.MemoryPitch) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Texture Alignment:  {0}", prop.TextureAlignment) & vbCrLf)

        Me.tbGPUCaps.AppendText(String.Format("   --- MP Information for device {0} ---", i) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Shared mem per mp: {0}", prop.SharedMemoryPerBlock) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Registers per mp:  {0}", prop.RegistersPerBlock) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Threads in warp:  {0}", prop.WarpSize) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Max threads per block:  {0}", prop.MaxThreadsPerBlock) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Max thread dimensions:  ({0}, {1}, {2})", prop.MaxThreadsSize.x, prop.MaxThreadsSize.y, prop.MaxThreadsSize.z) & vbCrLf)
        Me.tbGPUCaps.AppendText(String.Format("Max grid dimensions:  ({0}, {1}, {2})", prop.MaxGridSize.x, prop.MaxGridSize.y, prop.MaxGridSize.z) & vbCrLf)

        Me.tbGPUCaps.SelectionStart = 0
        Me.tbGPUCaps.SelectionLength = 0
        Me.tbGPUCaps.ScrollToCaret()

    End Sub

    Private Sub cbErrorHandlingMode_SelectedIndexChanged(sender As Object, e As EventArgs) Handles cbErrorHandlingMode.SelectedIndexChanged
        GlobalSettings.Settings.ExcelErrorHandlingMode = cbErrorHandlingMode.SelectedIndex
    End Sub

    Private Sub Button1_Click(sender As Object, e As EventArgs) Handles Button1.Click

        If GlobalSettings.Settings.ExcelFlashSettings <> "" Then
            flashsettings = Newtonsoft.Json.JsonConvert.DeserializeObject(Of Dictionary(Of Interfaces.Enums.FlashSetting, String))(GlobalSettings.Settings.ExcelFlashSettings)
        Else
            flashsettings = PropertyPackages.Auxiliary.FlashAlgorithms.FlashAlgorithm.GetDefaultSettings
        End If

        Dim f As New Thermodynamics.FlashAlgorithmConfig() With {.Settings = flashsettings, .ExcelMode = True}

        f.ShowDialog(Me)
        flashsettings = f.Settings

        GlobalSettings.Settings.ExcelFlashSettings = Newtonsoft.Json.JsonConvert.SerializeObject(flashsettings, Newtonsoft.Json.Formatting.Indented)
 
        f.Dispose()
        f = Nothing

    End Sub

End Class