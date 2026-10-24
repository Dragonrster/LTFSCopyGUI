Public Module Program

    <STAThread>
    Public Sub Main(args As String())
        Application.EnableVisualStyles()
        Application.SetCompatibleTextRenderingDefault(False)

        Dim form As New CmViewForm()
        If args IsNot Nothing AndAlso args.Length > 0 Then form.InitialFile = args(0)
        Application.Run(form)
    End Sub

End Module
