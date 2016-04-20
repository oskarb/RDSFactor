namespace RDSFactor
{
    partial class RDSFactor
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
            this.cleanupEvent = new System.Timers.Timer();
            ((System.ComponentModel.ISupportInitialize)(this.cleanupEvent)).BeginInit();
            // 
            // cleanupEvent
            // 
            this.cleanupEvent.Enabled = true;
            this.cleanupEvent.Elapsed += new System.Timers.ElapsedEventHandler(this.cleanupEvent_Elapsed);
            // 
            // RDSFactor
            // 
            this.ServiceName = "RDSFactor";
            ((System.ComponentModel.ISupportInitialize)(this.cleanupEvent)).EndInit();

        }

        #endregion


        public System.Timers.Timer cleanupEvent;
    }
}
