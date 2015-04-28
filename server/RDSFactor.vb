﻿Imports System.DirectoryServices
Imports System.IO
Imports System.Reflection
Imports RDSFactor.SMSModem
Imports RDSFactor.LogFile
Imports System.Security.Cryptography
Imports System.Text
Imports System
Imports System.Net.Mail
Imports RADAR

Public Class RDSFactor

    Public Shared LDAPDomain As String = ""
    Public Shared ADField As String = ""
    Public Shared ADMailField As String = ""
    Public Shared EnableOTP As Boolean
    Public Shared NetBiosDomain As String = ""
    Public Shared secrets As NASAuthList

    Public Shared SessionTimeOut As Integer = 30 ' in minutes
    Public Shared LaunchTimeOut As Integer = 30 ' in seconds
    Public Shared EnableSMS As Boolean = False
    Public Shared EnableEmail As Boolean = False

    Private Shared DEBUG As Boolean
    Private Shared UserAccessLog As New LogWriter
    Private Shared Log As New LogWriter

    Private server As RADIUSServer
    Private serverPort As Integer = 1812
    Private userHash As New Hashtable
    Private packetHash As New Hashtable
    Private clientHash As New Hashtable

    Private Shared Provider As String = ""
    Private Shared ModemType As String = ""
    Private Shared ComPort As String = ""
    Private Shared SmsC As String = ""
    Private Shared MailServer As String = ""
    Private Shared SenderEmail As String = ""
    Private TSGW As String = ""

    Protected Overrides Sub OnStart(ByVal args() As String)
        Log.filePath = ApplicationPath() & "\log.txt"
        UserAccessLog.filePath = ApplicationPath() & "\UserAccessLog.txt"

        Log.WriteLog("---------------------------------------------------------------------------------------------------")
        ServerLog("Starting Service")
        ServerLog("Loading Configuration...")
        loadConfiguration()
        ServerLog("Starting Radius listner ports...")
        StartUpServer()
    End Sub

    Protected Overrides Sub OnStop()
        ServerLog("Stopping Radius listner ports...")
    End Sub

    Public Sub StartUpServer()
        secrets = New NASAuthList

        For Each cl As DictionaryEntry In clientHash
            ServerLog("Adding Shared Secrets to Radius Server")
            secrets.AddSharedSecret(cl.Key, cl.Value)
        Next

        Try
            server = New RADIUSServer(serverPort, AddressOf ProcessPacket, secrets)
            ServerLog("Starting Radius Server on Port " & serverPort & " ...OK")
        Catch
            ServerLog("Starting Radius Server on Port " & serverPort & "...FAILED")
        End Try
    End Sub

    ' Every valid RADIUS request generated by the server(s) we created earlier
    ' will fire up the callback procedure. Invalid requests are dropped, per RFC.
    Private Sub ProcessPacket(ByVal packet As RADIUSPacket)
        If Not packet.IsValid Then
            Console.WriteLine("Packet is not valid. Discarding.")
            Exit Sub
        End If

        Dim handler = New RDSHandler(packet)

        ' If TSGW = "1" Then
        '   handler = New RDSHandler(packet)
        ' Else
        '   handler = New CitrixHandler(packet)
        ' End If

        handler.ProcessRequest()
    End Sub

    Public Shared Sub AccessLog(packet As RADIUSPacket, message As String)
        Dim from_address = packet.EndPoint.Address.ToString
        message = "[" & packet.UserName & " " & from_address & "] " & message
        AccessLog(message)
    End Sub

    Public Shared Sub AccessLog(message As String)
        message = Now & ": DEBUG: " & message
        If DEBUG = True Then
            UserAccessLog.WriteLog(message)

            ' Also write to the console if not a service
            If Environment.UserInteractive Then
                Console.WriteLine(message)
            End If
        End If
    End Sub

    Public Shared Sub ServerLog(ByVal message)
        message = Now & ": " & message
        Log.WriteLog(message)
        ' Also write to the console if not a service
        If Environment.UserInteractive Then
            Console.WriteLine(message)
        End If
    End Sub

    Public Shared Function GenerateCode() As String
        Dim dummy As Integer = 0

        Dim ordRand As New System.Random()
        Dim temp As New System.Collections.ArrayList()
        While temp.Count < 6
            dummy = ordRand.[Next](1, 9)
            If Not temp.Contains(dummy) Then
                temp.Add(dummy)
            End If
        End While
        Dim strVar As String = temp(0).ToString() + temp(1).ToString() + temp(2).ToString() + temp(3).ToString() + temp(4).ToString() + temp(5).ToString()
        Return strVar

    End Function

    Public Sub loadConfiguration()
        Dim ConfOk As Boolean = True
        Dim RConfig As New IniFile
        Try
            RConfig.Load(ApplicationPath() & "\conf\RDSFactor.ini")
            DEBUG = RConfig.GetKeyValue("RDSFactor", "Debug")
            NetBiosDomain = RConfig.GetKeyValue("RDSFactor", "NetBiosDomain")
            If NetBiosDomain.Length = 0 Then
                ServerLog("ERROR: NetBiosDomain can not be empty")
                ConfOk = False
            End If
            LDAPDomain = RConfig.GetKeyValue("RDSFactor", "LDAPDomain")
            If LDAPDomain.Length = 0 Then
                ServerLog("ERROR: LDAPDomain can not be empty")
                ConfOk = False
            End If

            TSGW = RConfig.GetKeyValue("RDSFactor", "TSGW")

            EnableOTP = RConfig.GetKeyValue("RDSFactor", "EnableOTP")

            If EnableOTP = True Then
                If RConfig.GetKeyValue("RDSFactor", "EnableEmail") = "1" Then
                    EnableEmail = True
                    SenderEmail = RConfig.GetKeyValue("RDSFactor", "SenderEmail")
                    MailServer = RConfig.GetKeyValue("RDSFactor", "MailServer")
                    ADMailField = RConfig.GetKeyValue("RDSFactor", "ADMailField")
                End If

                ADField = RConfig.GetKeyValue("RDSFactor", "ADField")
                If ADField.Length = 0 Then
                    ServerLog("ERROR:  ADField can not be empty")
                    ConfOk = False
                End If

                If RConfig.GetKeyValue("RDSFactor", "EnableSMS") = "1" Then
                    EnableSMS = True
                    ModemType = RConfig.GetKeyValue("RDSFactor", "USELOCALMODEM")
                    Select Case ModemType
                        Case "0"
                            Provider = RConfig.GetKeyValue("RDSFactor", "Provider")
                            If Provider.Length = 0 Then
                                ServerLog("ERROR:  Provider can not be empty")
                                ConfOk = False
                            End If
                        Case "1"
                            ComPort = RConfig.GetKeyValue("RDSFactor", "COMPORT")
                            If ComPort.Length = 0 Then
                                ServerLog("ERROR:  ComPort can not be empty")
                                ConfOk = False
                            End If
                            SmsC = RConfig.GetKeyValue("RDSFactor", "SMSC")
                            If SmsC.Length = 0 Then
                                ServerLog("ERROR:  SMSC can not be empty. See http://smsclist.com/downloads/default.txt for valid values")
                                ConfOk = False
                            End If
                        Case Else
                            ServerLog("ERROR:  USELOCALMODEM contain invalid configuration. Correct value are 1 or 0")
                            ConfOk = False
                    End Select
                End If

            End If

            Dim ClientList As String = ""
            ClientList = RConfig.GetKeyValue("RDSFactor", "ClientList")

            Dim ClientArray() As String
            ClientArray = Split(ClientList, ",")

            For i As Integer = 0 To ClientArray.Length - 1
                ServerLog("Loading Shared Secret for Client: " & ClientArray(i))
                clientHash.Add(ClientArray(i), RConfig.GetKeyValue("Clients", ClientArray(i)))
            Next

            If ConfOk = True Then
                ServerLog("Loading Configuration...OK")
            Else
                ServerLog("Loading Configuration...FAILED")
            End If
        Catch
            ServerLog("ERROR: Missing RDSFactor.ini from startup path or RDSFactor.ini contains invalid configuration")
            ServerLog("Loading Configuration...FAILED")
            End
        End Try
    End Sub

    Public Function ApplicationPath() As String
        Return Path.GetDirectoryName([Assembly].GetExecutingAssembly().Location)
    End Function

    Public Shared Function SendSMS(ByVal number As String, ByVal passcode As String) As String

        ' test if using online sms provider or local modem
        If ModemType = 1 Then ' local modem
            Dim modem As New SMSModem(ComPort)
            modem.Opens()
            modem.send(number, passcode, SmsC)
            modem.Closes()
            modem = Nothing
            Return "Ok"
        Else


            Dim baseurl As String = Provider.Split("?")(0)
            Dim client As New System.Net.WebClient()
            ' Add a user agent header in case the requested URI contains a query.

            client.Headers.Add("user-agent", "Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.2; .NET CLR1.0.3705;)")

            Dim parameters As String = Provider.Split("?")(1)
            Dim pary As String() = parameters.Split("&")

            For i As Integer = 0 To pary.Length - 1
                If pary(i).IndexOf("***TEXTMESSAGE***") > 0 Then
                    Dim qpar As String() = pary(i).Split("=")
                    client.QueryString.Add(qpar(0), passcode)
                ElseIf pary(i).IndexOf("***NUMBER***") > 0 Then
                    Dim qpar As String() = pary(i).Split("=")
                    client.QueryString.Add(qpar(0), number)
                Else

                    Dim qpar As String() = pary(i).Split("=")
                    client.QueryString.Add(qpar(0), qpar(1))
                End If
            Next


            Dim data As Stream = client.OpenRead(baseurl)
            Dim reader As New StreamReader(data)
            Dim s As String = reader.ReadToEnd()
            data.Close()
            reader.Close()
            Return (s)
        End If

    End Function

    Public Shared Function SendEmail(email As String, passcode As String) As String


        Dim mail As New MailMessage()
        mail.To.Add(email)
        mail.From = New MailAddress(SenderEmail)
        mail.Subject = "Token: " & passcode
        mail.Body = "Subject contains the token code to login to you site"
        mail.IsBodyHtml = False
        Dim smtp As New SmtpClient(MailServer)


        Try
            smtp.Send(mail)
            If DEBUG = True Then
                AccessLog(Now & ": Mail send to: " & email)
            End If
            Return "SEND"
        Catch e As InvalidCastException

            If DEBUG = True Then
                AccessLog(Now & " : Debug: " & e.Message)
                AccessLog(Now & " : Unable to send mail to: " & email & "  ## Check that MAILSERVER and SENDEREMAIL are configured correctly in smscode.conf. Also check that your Webinterface server is allowed to relay through the mail server specified")
            End If
            Return "FAILED"
        End Try
    End Function

    Public Sub CleanupEventHandler(sender, e) Handles cleanupEvent.Elapsed
        RDSHandler.Cleanup()
    End Sub

End Class