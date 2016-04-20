namespace RDSFactor
{
    partial class ProjectInstaller
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.ServiceProcessInstaller1 = new System.ServiceProcess.ServiceProcessInstaller();
            this.ServiceInstaller1 = new System.ServiceProcess.ServiceInstaller();
            // 
            // ServiceProcessInstaller1
            // 
            this.ServiceProcessInstaller1.Account = System.ServiceProcess.ServiceAccount.LocalSystem;
            this.ServiceProcessInstaller1.Password = null;
            this.ServiceProcessInstaller1.Username = null;
            // 
            // ServiceInstaller1
            // 
            this.ServiceInstaller1.Description = "RDSFactor Radius Server";
            this.ServiceInstaller1.DisplayName = "RDSFactor Radius Server";
            this.ServiceInstaller1.ServiceName = "RDSFactor";
            this.ServiceInstaller1.StartType = System.ServiceProcess.ServiceStartMode.Automatic;
            // 
            // ProjectInstaller
            // 
            this.Installers.AddRange(new System.Configuration.Install.Installer[] {
            this.ServiceProcessInstaller1,
            this.ServiceInstaller1});

        }

        #endregion

        System.ServiceProcess.ServiceProcessInstaller ServiceProcessInstaller1;
        System.ServiceProcess.ServiceInstaller ServiceInstaller1;
    }
}