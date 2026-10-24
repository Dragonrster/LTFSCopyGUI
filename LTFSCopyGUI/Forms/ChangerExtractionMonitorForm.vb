Imports System
Imports System.Collections.Generic
Imports System.Drawing
Imports System.Globalization
Imports System.Threading
Imports System.Windows.Forms

''' <summary>提取监控窗口中每个参与驱动器的运行状态。</summary>
Friend Class ChangerExtractionMonitorDriveState
    Friend Index As Integer
    Friend LabelText As String = String.Empty
    Friend ChartSpeed As Direct2DChartControl
    Friend ChartError As Direct2DChartControl
    Friend SpeedLabel As Label
    Friend ErrorLabel As Label
    Friend TapeLabel As Label
    Friend Handle As IntPtr
    Friend TotalBytes As Long
    Friend LastBytes As Long
    Friend ErrorReader As ChangerFileManagerCore.HeadErrorRateReader = New ChangerFileManagerCore.HeadErrorRateReader()
End Class

''' <summary>
''' 提取过程监控窗口：每个参与提取的驱动器一格，显示实时提取速度和磁头错误率。
''' 由 ChangerFileManagerForm 在提取开始时创建并显示，提取结束时保持窗口供查看。
''' </summary>
Public Class ChangerExtractionMonitorForm
    Inherits Form

    Private Const SpeedChartSamples As Integer = 300
    Private Const ErrorChartSamples As Integer = 300

    Private ReadOnly _states As New List(Of ChangerExtractionMonitorDriveState)()
    Private ReadOnly _stateLock As New Object()
    Private _timer As System.Windows.Forms.Timer
    Private _headerLabel As Label
    Private _finished As Boolean

    Public Sub New(driveLabels As IList(Of String))
        Me.Text = "提取监控 - 速度与磁头错误率"
        Me.StartPosition = FormStartPosition.CenterParent
        Me.Size = New Size(980, 700)
        Me.MinimizeBox = True
        Me.MaximizeBox = True
        Me.ShowInTaskbar = False
        Me.Font = DisplayHelper.DisplayFont

        Dim header As New Label() With {
            .Text = "提取速度与磁头错误率（每秒刷新，每个驱动器一格）",
            .Dock = DockStyle.Top,
            .Height = 26,
            .TextAlign = ContentAlignment.MiddleLeft,
            .Padding = New Padding(8, 4, 0, 0)
        }
        _headerLabel = header

        Dim scrollPanel As New Panel() With {
            .Dock = DockStyle.Fill,
            .AutoScroll = True
        }
        Dim layout As New TableLayoutPanel() With {
            .Dock = DockStyle.Top,
            .ColumnCount = 1,
            .AutoSize = True,
            .AutoSizeMode = AutoSizeMode.GrowAndShrink
        }
        Dim labelCount As Integer = If(driveLabels Is Nothing, 0, driveLabels.Count)
        For index As Integer = 0 To labelCount - 1
            layout.Controls.Add(BuildDriveGroup(index, driveLabels(index)))
        Next
        scrollPanel.Controls.Add(layout)

        Me.Controls.Add(scrollPanel)
        Me.Controls.Add(header)

        _timer = New System.Windows.Forms.Timer() With {.Interval = 1000}
        AddHandler _timer.Tick, AddressOf MonitorTick
        AddHandler Me.Shown, Sub(sender As Object, e As EventArgs) _timer.Start()
        AddHandler Me.FormClosed, Sub(sender As Object, e As EventArgs) _timer.Stop()
    End Sub

    Private Function BuildDriveGroup(index As Integer, labelText As String) As GroupBox
        Dim group As New GroupBox() With {
            .Text = $"驱动器 {index + 1}：{labelText}",
            .Dock = DockStyle.Top,
            .Height = 178,
            .Padding = New Padding(8)
        }
        Dim tapeLabel As New Label() With {
            .Location = New Point(12, 22),
            .AutoSize = True,
            .Text = "当前磁带：—"
        }
        Dim speedLabel As New Label() With {
            .Location = New Point(12, 44),
            .AutoSize = True,
            .Text = "速度：0.00 MiB/s"
        }
        Dim errorLabel As New Label() With {
            .Location = New Point(300, 44),
            .AutoSize = True,
            .Text = "磁头错误率：—"
        }
        Dim speedChart As New Direct2DChartControl() With {
            .Location = New Point(12, 68),
            .Size = New Size(456, 98),
            .ChartTitle = "提取速度",
            .PrimaryAxisTitle = "MiB/s",
            .SecondaryAxisTitle = "",
            .VisibleSamples = SpeedChartSamples
        }
        Dim errorChart As New Direct2DChartControl() With {
            .Location = New Point(480, 68),
            .Size = New Size(456, 98),
            .ChartTitle = "磁头错误率（1E-6 ~ 1）",
            .PrimaryAxisTitle = "错误率（对数刻度）",
            .SecondaryAxisTitle = "",
            .VisibleSamples = ErrorChartSamples
        }
        group.Controls.Add(tapeLabel)
        group.Controls.Add(speedLabel)
        group.Controls.Add(errorLabel)
        group.Controls.Add(speedChart)
        group.Controls.Add(errorChart)

        Dim state As New ChangerExtractionMonitorDriveState() With {
            .Index = index,
            .LabelText = labelText,
            .ChartSpeed = speedChart,
            .ChartError = errorChart,
            .SpeedLabel = speedLabel,
            .ErrorLabel = errorLabel,
            .TapeLabel = tapeLabel
        }
        SyncLock _stateLock
            _states.Add(state)
        End SyncLock
        Return group
    End Function

    Private Sub MonitorTick(sender As Object, e As EventArgs)
        Dim statesSnapshot As New List(Of ChangerExtractionMonitorDriveState)()
        SyncLock _stateLock
            statesSnapshot.AddRange(_states)
        End SyncLock
        For Each state In statesSnapshot
            Dim handle As IntPtr
            Dim total As Long
            SyncLock _stateLock
                handle = state.Handle
                total = Interlocked.Read(state.TotalBytes)
            End SyncLock
            Dim delta As Long = total - state.LastBytes
            If delta < 0L Then delta = 0L
            state.LastBytes = total
            Dim mibPerSecond As Double = delta / 1048576.0
            SafeAppendSample(state.ChartSpeed, mibPerSecond)
            state.SpeedLabel.Text = $"速度：{mibPerSecond.ToString("0.00", CultureInfo.InvariantCulture)} MiB/s    累计：{FormatSize(total)}"

            If handle <> IntPtr.Zero AndAlso handle <> New IntPtr(-1) Then
                Dim logRate As Double = 0
                If state.ErrorReader.TryRead(handle, logRate) Then
                    SafeAppendSample(state.ChartError, Math.Max(logRate, -6.0) + 6.0)
                    state.ErrorLabel.Text = "磁头错误率：" & FormatErrorRate(logRate)
                    state.ErrorLabel.ForeColor = ErrorRateColor(logRate)
                End If
            ElseIf Not _finished Then
                state.ErrorLabel.Text = "磁头错误率：—"
            End If
        Next
    End Sub

    Private Shared Sub SafeAppendSample(chart As Direct2DChartControl, value As Double)
        If chart Is Nothing OrElse chart.IsDisposed Then Return
        Try
            chart.AppendSample(value, 0)
        Catch ex As Exception
        End Try
    End Sub

    Private Shared Function FormatErrorRate(logRate As Double) As String
        If logRate >= 0 Then Return "0（无错误）"
        Dim rate As Double = Math.Pow(10.0, logRate)
        Return rate.ToString("0.###E+00", CultureInfo.InvariantCulture)
    End Function

    Private Shared Function ErrorRateColor(logRate As Double) As Color
        If logRate >= 0 Then Return Color.FromArgb(101, 225, 111)
        Dim text As String = logRate.ToString("0.00", CultureInfo.InvariantCulture)
        Return ErrRateHelper.GetColor(text)
    End Function

    Private Shared Function FormatSize(bytes As Long) As String
        If bytes < 0L Then Return "0 Bytes"
        If bytes < 1024L Then Return $"{bytes} Bytes"
        Dim value As Double = bytes
        Dim units As String() = {"KiB", "MiB", "GiB", "TiB", "PiB"}
        Dim unitIndex As Integer = -1
        While value >= 1024.0 AndAlso unitIndex < units.Length - 1
            value /= 1024.0
            unitIndex += 1
        End While
        If unitIndex < 0 Then Return $"{bytes} Bytes"
        Return $"{value.ToString("0.00", CultureInfo.InvariantCulture)} {units(unitIndex)}"
    End Function

    ''' <summary>由提取线程调用：累加某个驱动器已提取的字节数。</summary>
    Friend Sub ReportBytes(index As Integer, byteCount As Long)
        If index < 0 OrElse index >= _states.Count Then Return
        Interlocked.Add(_states(index).TotalBytes, byteCount)
    End Sub

    ''' <summary>由提取线程调用：绑定/解绑某个驱动器的磁带句柄（IntPtr.Zero 表示已关闭）。</summary>
    Friend Sub AttachHandle(index As Integer, handle As IntPtr)
        If index < 0 OrElse index >= _states.Count Then Return
        Dim state As ChangerExtractionMonitorDriveState = _states(index)
        SyncLock _stateLock
            state.Handle = handle
            state.LastBytes = Interlocked.Read(state.TotalBytes)
        End SyncLock
        If handle = IntPtr.Zero AndAlso Not _finished Then
            If Not IsDisposed AndAlso IsHandleCreated Then
                Try
                    BeginInvoke(Sub() state.ErrorLabel.Text = "磁头错误率：—")
                Catch ex As Exception
                End Try
            End If
        End If
    End Sub

    ''' <summary>由提取线程调用：标记某个驱动器当前正在处理的磁带。</summary>
    Friend Sub SetCurrentTape(index As Integer, tapeBarcode As String)
        If index < 0 OrElse index >= _states.Count Then Return
        If IsDisposed OrElse Not IsHandleCreated Then Return
        If InvokeRequired Then
            Try
                BeginInvoke(Sub() SetCurrentTape(index, tapeBarcode))
            Catch ex As Exception
            End Try
            Return
        End If
        _states(index).TapeLabel.Text = "当前磁带：" & If(String.IsNullOrWhiteSpace(tapeBarcode), "—", tapeBarcode)
    End Sub

    ''' <summary>由提取线程调用：标记某个驱动器当前空闲（无磁带处理）。</summary>
    Friend Sub MarkIdle(index As Integer)
        If index < 0 OrElse index >= _states.Count Then Return
        AttachHandle(index, IntPtr.Zero)
    End Sub

    ''' <summary>由界面线程调用：全部驱动器都完成/进入空闲。</summary>
    Friend Sub MarkAllIdle()
        SyncLock _stateLock
            For Each state In _states
                state.Handle = IntPtr.Zero
            Next
        End SyncLock
    End Sub

    ''' <summary>提取结束时更新标题与状态行。</summary>
    Friend Sub MarkFinished(summary As String)
        If IsDisposed OrElse Not IsHandleCreated Then Return
        If InvokeRequired Then
            Try
                BeginInvoke(Sub() MarkFinished(summary))
            Catch ex As Exception
            End Try
            Return
        End If
        _finished = True
        MarkAllIdle()
        Me.Text = "提取监控 - 提取已结束"
        _headerLabel.Text = If(String.IsNullOrWhiteSpace(summary), "提取已结束", summary)
    End Sub
End Class
