namespace AowEmailWrapper.Controls
{
    partial class AutoconfigPage1Welcome
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
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(AutoconfigPage1Welcome));
            this.lblAutoconfigPage1WrapperMessage = new System.Windows.Forms.Label();
            this.lblAutoconfigPage1AuthMessage = new System.Windows.Forms.Label();
            this.groupBoxAccount = new System.Windows.Forms.GroupBox();
            this.fbPassword = new AowEmailWrapper.Controls.FormBlockText();
            this.fbEmailAddress = new AowEmailWrapper.Controls.FormBlockText();
            this.linkPasswordHint = new System.Windows.Forms.LinkLabel();
            this.buttonSignInMicrosoft = new System.Windows.Forms.Button();
            this.groupBoxAccount.SuspendLayout();
            this.SuspendLayout();
            // 
            // lblAutoconfigPage1WrapperMessage
            // 
            this.lblAutoconfigPage1WrapperMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblAutoconfigPage1WrapperMessage.Location = new System.Drawing.Point(0, 0);
            this.lblAutoconfigPage1WrapperMessage.Name = "lblAutoconfigPage1WrapperMessage";
            this.lblAutoconfigPage1WrapperMessage.Size = new System.Drawing.Size(431, 40);
            this.lblAutoconfigPage1WrapperMessage.TabIndex = 0;
            this.lblAutoconfigPage1WrapperMessage.Text = resources.GetString("lblAutoconfigPage1WrapperMessage.Text");
            // 
            // lblAutoconfigPage1AuthMessage
            // 
            this.lblAutoconfigPage1AuthMessage.Dock = System.Windows.Forms.DockStyle.Top;
            this.lblAutoconfigPage1AuthMessage.Location = new System.Drawing.Point(0, 100);
            this.lblAutoconfigPage1AuthMessage.Name = "lblAutoconfigPage1AuthMessage";
            this.lblAutoconfigPage1AuthMessage.Size = new System.Drawing.Size(431, 20);
            this.lblAutoconfigPage1AuthMessage.TabIndex = 1;
            this.lblAutoconfigPage1AuthMessage.Text = "Please provide the authentication details for your email account.";
            // 
            // groupBoxAccount
            // 
            this.groupBoxAccount.Controls.Add(this.buttonSignInMicrosoft);
            this.groupBoxAccount.Controls.Add(this.fbPassword);
            this.groupBoxAccount.Controls.Add(this.fbEmailAddress);
            this.groupBoxAccount.Dock = System.Windows.Forms.DockStyle.Top;
            this.groupBoxAccount.Location = new System.Drawing.Point(0, 120);
            this.groupBoxAccount.Name = "groupBoxAccount";
            this.groupBoxAccount.Padding = new System.Windows.Forms.Padding(3, 0, 3, 3);
            this.groupBoxAccount.Size = new System.Drawing.Size(431, 74);
            this.groupBoxAccount.TabIndex = 13;
            this.groupBoxAccount.TabStop = false;
            this.groupBoxAccount.Text = "Your account";
            // 
            // fbPassword
            // 
            this.fbPassword.Dock = System.Windows.Forms.DockStyle.Top;
            this.fbPassword.IsPassword = true;
            this.fbPassword.LabelName = "Password:";
            this.fbPassword.Location = new System.Drawing.Point(3, 37);
            this.fbPassword.Margin = new System.Windows.Forms.Padding(2);
            this.fbPassword.MinimumSize = new System.Drawing.Size(0, 24);
            this.fbPassword.Name = "fbPassword";
            this.fbPassword.Size = new System.Drawing.Size(425, 24);
            this.fbPassword.TabIndex = 1;
            this.fbPassword.TextValue = "";
            this.fbPassword.ValidationRegEx = "^\\S+.*";
            // 
            // fbEmailAddress
            // 
            this.fbEmailAddress.Dock = System.Windows.Forms.DockStyle.Top;
            this.fbEmailAddress.IsPassword = false;
            this.fbEmailAddress.LabelName = "Email Address:";
            this.fbEmailAddress.Location = new System.Drawing.Point(3, 13);
            this.fbEmailAddress.Margin = new System.Windows.Forms.Padding(2);
            this.fbEmailAddress.MinimumSize = new System.Drawing.Size(0, 24);
            this.fbEmailAddress.Name = "fbEmailAddress";
            this.fbEmailAddress.Size = new System.Drawing.Size(425, 24);
            this.fbEmailAddress.TabIndex = 0;
            this.fbEmailAddress.TextValue = "";
            this.fbEmailAddress.ValidationRegEx = resources.GetString("fbEmailAddress.ValidationRegEx");
            // 
            // AutoconfigPage1Welcome
            // 
            // 
            // buttonSignInMicrosoft
            // 
            this.buttonSignInMicrosoft.Dock = System.Windows.Forms.DockStyle.Top;
            this.buttonSignInMicrosoft.Name = "buttonSignInMicrosoft";
            this.buttonSignInMicrosoft.Size = new System.Drawing.Size(425, 26);
            this.buttonSignInMicrosoft.TabIndex = 2;
            this.buttonSignInMicrosoft.Text = "Sign in with Microsoft";
            this.buttonSignInMicrosoft.UseVisualStyleBackColor = true;
            this.buttonSignInMicrosoft.Visible = false;
            // 
            // linkPasswordHint
            // 
            this.linkPasswordHint.Dock = System.Windows.Forms.DockStyle.Top;
            this.linkPasswordHint.LinkArea = new System.Windows.Forms.LinkArea(0, 0);
            this.linkPasswordHint.Name = "linkPasswordHint";
            this.linkPasswordHint.Padding = new System.Windows.Forms.Padding(0, 6, 0, 0);
            this.linkPasswordHint.Size = new System.Drawing.Size(431, 84);
            this.linkPasswordHint.TabIndex = 14;
            this.linkPasswordHint.TabStop = false;
            this.linkPasswordHint.Text = "";
            this.linkPasswordHint.Visible = false;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.Transparent;
            this.Controls.Add(this.linkPasswordHint);
            this.Controls.Add(this.groupBoxAccount);
            this.Controls.Add(this.lblAutoconfigPage1AuthMessage);
            this.Controls.Add(this.lblAutoconfigPage1WrapperMessage);
            this.Name = "AutoconfigPage1Welcome";
            this.Size = new System.Drawing.Size(431, 253);
            this.groupBoxAccount.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Label lblAutoconfigPage1WrapperMessage;
        private System.Windows.Forms.Label lblAutoconfigPage1AuthMessage;
        private System.Windows.Forms.GroupBox groupBoxAccount;
        private FormBlockText fbPassword;
        private FormBlockText fbEmailAddress;
    }
}
