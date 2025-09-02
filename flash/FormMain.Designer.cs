
namespace flash
{
    partial class FormMain
    {
        /// <summary>
        /// 設計工具所需的變數。
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// 清除任何使用中的資源。
        /// </summary>
        /// <param name="disposing">如果應該處置受控資源則為 true，否則為 false。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form 設計工具產生的程式碼

        /// <summary>
        /// 此為設計工具支援所需的方法 - 請勿使用程式碼編輯器修改
        /// 這個方法的內容。
        /// </summary>
        private void InitializeComponent()
        {
            this.FlashOff = new System.Windows.Forms.Button();
            this.FlashOn = new System.Windows.Forms.Button();
            this.comboBox1 = new System.Windows.Forms.ComboBox();
            this.SuspendLayout();
            // 
            // FlashOff
            // 
            this.FlashOff.Location = new System.Drawing.Point(153, 43);
            this.FlashOff.Margin = new System.Windows.Forms.Padding(2);
            this.FlashOff.Name = "FlashOff";
            this.FlashOff.Size = new System.Drawing.Size(75, 47);
            this.FlashOff.TabIndex = 3;
            this.FlashOff.Text = "Flash Off";
            this.FlashOff.UseVisualStyleBackColor = true;
            this.FlashOff.Click += new System.EventHandler(this.FlashOff_Click);
            // 
            // FlashOn
            // 
            this.FlashOn.Location = new System.Drawing.Point(21, 43);
            this.FlashOn.Margin = new System.Windows.Forms.Padding(2);
            this.FlashOn.Name = "FlashOn";
            this.FlashOn.Size = new System.Drawing.Size(64, 47);
            this.FlashOn.TabIndex = 2;
            this.FlashOn.Text = "Flash On";
            this.FlashOn.UseVisualStyleBackColor = true;
            this.FlashOn.Click += new System.EventHandler(this.FlashOn_Click);
            // 
            // comboBox1
            // 
            this.comboBox1.FormattingEnabled = true;
            this.comboBox1.Location = new System.Drawing.Point(75, 111);
            this.comboBox1.Margin = new System.Windows.Forms.Padding(2);
            this.comboBox1.Name = "comboBox1";
            this.comboBox1.Size = new System.Drawing.Size(82, 20);
            this.comboBox1.TabIndex = 4;
            // 
            // FormMain
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 12F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(250, 146);
            this.Controls.Add(this.comboBox1);
            this.Controls.Add(this.FlashOff);
            this.Controls.Add(this.FlashOn);
            this.Margin = new System.Windows.Forms.Padding(2);
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "FormMain";
            this.Opacity = 0D;
            this.ShowIcon = false;
            this.ShowInTaskbar = false;
            this.Text = "flash";
            this.Load += new System.EventHandler(this.FormMain_Load);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button FlashOff;
        private System.Windows.Forms.Button FlashOn;
        private System.Windows.Forms.ComboBox comboBox1;
    }
}

