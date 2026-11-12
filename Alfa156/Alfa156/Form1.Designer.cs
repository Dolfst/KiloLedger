namespace CarExpenses
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null)
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            AutoScroll = false;
            BackColor = System.Drawing.Color.FromArgb(232, 238, 244);
            ClientSize = new System.Drawing.Size(240, 268);
            Name = "Form1";
            Text = "Учет расходов";
            WindowState = System.Windows.Forms.FormWindowState.Maximized;
        }
    }
}
