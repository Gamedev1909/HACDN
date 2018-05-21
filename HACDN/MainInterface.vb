Imports System.IO
Imports System.IO.File
Imports System.Net
Imports System.Security.Cryptography.X509Certificates
Imports System.Text.RegularExpressions
Imports System.Text
Imports System.Threading
Public Class Form1
    Dim SW As Stopwatch
    ' Allow self-signed certificates
    Public Function AcceptAllCertifications(ByVal sender As Object, ByVal certification As System.Security.Cryptography.X509Certificates.X509Certificate, ByVal chain As System.Security.Cryptography.X509Certificates.X509Chain, ByVal sslPolicyErrors As System.Net.Security.SslPolicyErrors) As Boolean
        Return True
    End Function
    Private Function ConvertToASCIIUsingRegex(inputValue As String) As String
        ' Remove all disallowed filename characters (\/:*?:<>|)
        Return Regex.Replace(inputValue, "[^\w ]", String.Empty)
    End Function
    Public Shared Function ByteArrayToString(ByVal ByteIn As Byte()) As String
        ' Convert a byte array to hex string
        Dim HexIn As StringBuilder = New StringBuilder(ByteIn.Length * 2)
        For Each ByteVal As Byte In ByteIn
            HexIn.AppendFormat("{0:x2}", ByteVal)
        Next
        Return HexIn.ToString()
    End Function
    Public Class WebClient2
        ' Make a new WebClient that allows client certificates (seriously, Microsoft?)
        Inherits System.Net.WebClient

        Private _ClientCertificates As New System.Security.Cryptography.X509Certificates.X509CertificateCollection
        Public ReadOnly Property ClientCertificates() As System.Security.Cryptography.X509Certificates.X509CertificateCollection
            Get
                Return Me._ClientCertificates
            End Get
        End Property
        Protected Overrides Function GetWebRequest(ByVal address As System.Uri) As System.Net.WebRequest
            Dim ContactURL = MyBase.GetWebRequest(address)
            If TypeOf ContactURL Is HttpWebRequest Then
                Dim WR = DirectCast(ContactURL, HttpWebRequest)
                If Me._ClientCertificates IsNot Nothing AndAlso Me._ClientCertificates.Count > 0 Then
                    WR.ClientCertificates.AddRange(Me._ClientCertificates)
                End If
            End If
            Return ContactURL
        End Function
    End Class
    Public Sub Client_ProgressChanged(ByVal sender As Object, ByVal e As System.Net.DownloadProgressChangedEventArgs)
        ' Display the status bar
        Status_Bar.Visible = True
        ' Get the amount of bytes downloaded
        Dim bytesIn As Double = Double.Parse(e.BytesReceived.ToString())
        ' Convert to Mebibyte
        Dim asMiB As Double = bytesIn / 1048576
        ' Convert to Gibibyte
        Dim asGiB As Double = bytesIn / 1073741824
        ' Get the total filesize as bytes
        Dim totalBytes As Double = Double.Parse(e.TotalBytesToReceive.ToString())
        ' Convert to Mebibyte
        Dim totalasMiB As Double = totalBytes / 1048576
        ' Convert to Gibibyte
        Dim totalasGiB As Double = totalBytes / 1073741824
        ' Calculate percentage complete
        Dim percentage As Double = bytesIn / totalBytes * 100
        Dim speed As Double = Double.Parse(e.BytesReceived / SW.ElapsedMilliseconds)
        ToolStripStatusLabel1.Text = Format$(speed, "0") + "KB/sec"

        ' If over 1GiB, diplay "GB", else display as "MB" (damn it, Microsoft, make the difference between a GB and a GiB clear, you fooled the masses)
        If asMiB > 1024 Then
            AmountDownloaded.Text = Format$(asGiB, "0.00") + "GB"
        Else
            AmountDownloaded.Text = Format$(asMiB, "0.0") + "MB"
        End If
        ' Same as above but for total filesize
        If totalasMiB > 1024 Then
            TotalFileSize.Text = Format$(totalasGiB, "0.00") + "GB"
        Else
            TotalFileSize.Text = Format$(totalasMiB, "0.0") + "MB"
        End If
        ' Display the download percentage
        PercentDownloaded.Text = Format$(percentage, "0.0") + "% done"
    End Sub
    Private Sub client_DownloadCompleted(ByVal sender As Object, ByVal e As System.ComponentModel.AsyncCompletedEventArgs)
        MsgBox("Download complete.")
        Directory.Delete("Cont", True)
        Close()
    End Sub
    Public Sub Button2_Click(sender As Object, e As EventArgs) Handles DownloadButton.Click
        Dim process As System.Diagnostics.Process = Nothing
        Dim processStartInfo As System.Diagnostics.ProcessStartInfo
        Try
            ' Makes sure the device ID is present
            If DID_Input.Text = "" Then
                MsgBox("You must input your device ID.")
            Else
                ' Check to see if these files are present
                If Exists("nx_tls_client_cert.pfx") = True AndAlso Exists("hactool.exe") = True AndAlso Exists("keys.txt") = True Then
                    ServicePointManager.ServerCertificateValidationCallback = AddressOf AcceptAllCertifications
                    Dim ClientCert As New X509Certificate2("nx_tls_client_cert.pfx", "switch")
                    Dim TID As String = TitleID_Input.Text
                    Dim VER As String = Version_Input.Text
                    Dim DID As String = DID_Input.Text
                    Dim MetaURL As String = "https://atum.hac.lp1.d4c.nintendo.net/t/a/" + TID + "/" + VER
                    Dim GetMetaNCAID As HttpWebRequest = WebRequest.Create(MetaURL)
                    GetMetaNCAID.ClientCertificates.Add(ClientCert)
                    GetMetaNCAID.Method = "HEAD"
                    GetMetaNCAID.UserAgent = "NintendoSDK Firmware/5.0.2-0 (platform:NX; did:" + DID + "; eid:lp1)"
                    Dim ParseMetaNCAID As HttpWebResponse = GetMetaNCAID.GetResponse
                    Dim MetaNCAURL As String = "https://atum.hac.lp1.d4c.nintendo.net/c/a/" + ParseMetaNCAID.GetResponseHeader("x-nintendo-content-id")
                    Dim GetMetaNCA As HttpWebRequest = WebRequest.Create(MetaNCAURL)
                    GetMetaNCA.ClientCertificates.Add(ClientCert)
                    GetMetaNCA.UserAgent = "NintendoSDK Firmware/5.0.2-0 (platform:NX; did:" + DID + "; eid:lp1)"
                    Dim ParseMetaNCA As HttpWebResponse = GetMetaNCA.GetResponse
                    Dim MetaNCA As BinaryReader = New BinaryReader(ParseMetaNCA.GetResponseStream)
                    WriteAllBytes(VER, MetaNCA.ReadBytes(100000))
                    ProcessStartInfo = New System.Diagnostics.ProcessStartInfo()
                    ProcessStartInfo.FileName = "hactool.exe"
                    ProcessStartInfo.Arguments = " -k keys.txt " + VER + " --section0dir=CNMT"
                    ProcessStartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                    ProcessStartInfo.UseShellExecute = True
                    Process = System.Diagnostics.Process.Start(ProcessStartInfo)
                    process.Start()
                    Dim OpenCNMT As New System.IO.BinaryReader(File.Open("CNMT/Application_" + TID + ".cnmt", FileMode.Open))
                    Dim ParseCNMT As String = ByteArrayToString(OpenCNMT.ReadBytes(1000))
                    Dim ContNCAID As String
                    If ParseCNMT.Substring(32, 2) = "03" Then
                        If ParseCNMT.Substring(316, 2) = "03" Then
                            ContNCAID = ParseCNMT.Substring(272, 32)
                        ElseIf ParseCNMT.Substring(428, 2) = "03" Then
                            ContNCAID = ParseCNMT.Substring(384, 32)
                        End If
                    ElseIf ParseCNMT.Substring(32, 2) = "04" Then
                        If ParseCNMT.Substring(316, 2) = "03" Then
                            ContNCAID = ParseCNMT.Substring(272, 32)
                        ElseIf ParseCNMT.Substring(428, 2) = "03" Then
                            ContNCAID = ParseCNMT.Substring(384, 32)
                        ElseIf ParseCNMT.Substring(540, 2) = "03" Then
                            ContNCAID = ParseCNMT.Substring(496, 32)
                        End If
                    End If
                    Dim ContNCAURL As String = "https://atum.hac.lp1.d4c.nintendo.net/c/c/" + ContNCAID
                        Dim GetContNCA As HttpWebRequest = WebRequest.Create(ContNCAURL)
                        GetContNCA.ClientCertificates.Add(ClientCert)
                        GetContNCA.UserAgent = "NintendoSDK Firmware/5.0.2-0 (platform:NX; did:" + DID + "; eid:lp1)"
                        Dim ParseContNCA As HttpWebResponse = GetContNCA.GetResponse
                        Dim ContNCA As BinaryReader = New BinaryReader(ParseContNCA.GetResponseStream)
                        WriteAllBytes("TempCont", ContNCA.ReadBytes(10000000))
                        processStartInfo = New System.Diagnostics.ProcessStartInfo()
                        processStartInfo.FileName = "hactool.exe"
                        processStartInfo.Arguments = " -k keys.txt " + "TempCont" + " --section0dir=Cont"
                        processStartInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden
                        processStartInfo.UseShellExecute = True
                        process = System.Diagnostics.Process.Start(processStartInfo)
                        process.Start()
                        OpenCNMT.Close()
                    Thread.Sleep(1000)
                    Dim OpenCNMT2 As New System.IO.BinaryReader(File.Open("CNMT/Application_" + TID + ".cnmt", FileMode.Open))
                        Dim ParseCNMT2 As String = ByteArrayToString(OpenCNMT2.ReadBytes(1000))
                        Dim ReadControl As New String(File.ReadAllText("Cont/control.nacp"))
                        Dim NCAID As String = ParseCNMT2.Substring(160, 32)
                        Dim NCAURL As String = "https://atum.hac.lp1.d4c.nintendo.net/c/c/" + NCAID
                        Dim GameName As String = ReadControl.Substring(0, 128).Trim
                        Dim GetNCA As New WebClient2
                        GetNCA.ClientCertificates.Add(ClientCert)
                        GetNCA.Headers.Set("User-Agent", "NintendoSDK Firmware/5.0.2-0 (platform:NX; did:" + DID + "; eid:lp1)")
                        Dim Adr As New Uri(NCAURL)
                        AddHandler GetNCA.DownloadProgressChanged, AddressOf Client_ProgressChanged
                        AddHandler GetNCA.DownloadFileCompleted, AddressOf client_DownloadCompleted
                        Directory.CreateDirectory("Games/" + ConvertToASCIIUsingRegex(GameName))
                        GetNCA.DownloadFileTaskAsync(Adr, ("Games/" + ConvertToASCIIUsingRegex(GameName) + "/" + NCAID + ".nca"))
                        SW = Stopwatch.StartNew
                        Me.Text = ("Downloading " + ConvertToASCIIUsingRegex(GameName) + "...")
                    Else
                        ' If one of the files aren't present
                        MsgBox("This function requires your console-unique client cert (nx_tls_client_cert.pfx), hactool and a filled keys.txt file.")
                End If
            End If


        Catch ex As WebException
            MsgBox("Invalid title ID.")
        End Try
    End Sub
    Private Sub Form1_DragDrop(sender As Object, e As DragEventArgs) Handles MyBase.DragDrop
        ' Derives your device ID from a drag-and-dropped PRODINFO file
        Dim DraggedFile() As String = e.Data.GetData(DataFormats.FileDrop)
        For Each File In DraggedFile
            DID_Input.Text = ReadAllText(File).Substring(1342, 32).Replace("N", "").Replace("X", "").Replace("-0", "").Replace(Chr(2), "").Replace(Chr(0), "")
        Next
    End Sub

    Private Sub Form1_DragEnter(sender As Object, e As DragEventArgs) Handles MyBase.DragEnter
        ' Gets the drag and drop working
        If e.Data.GetDataPresent(DataFormats.FileDrop) Then
            e.Effect = DragDropEffects.Copy
        End If
    End Sub

    Private Sub Form1_DragLeave(sender As Object, e As EventArgs) Handles MyBase.DragLeave
        ' Don't really need to put anything here...
    End Sub

    Private Sub Form1_Load(sender As Object, e As EventArgs) Handles MyBase.Load
        ' Allow dragging-and-dropping
        Me.AllowDrop = True
    End Sub
End Class