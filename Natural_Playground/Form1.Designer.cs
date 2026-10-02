namespace Natural_Playground
{
    partial class Form1
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();

            // TabControl
            this.tabMain = new System.Windows.Forms.TabControl();
            this.tabApInt = new System.Windows.Forms.TabPage();
            this.tabApFloat = new System.Windows.Forms.TabPage();

            // ---------------------------------------------------------
            // ApInt Tab Controls
            // ---------------------------------------------------------
            this.splitIntMain = new System.Windows.Forms.SplitContainer();
            this.pnlIntTop = new System.Windows.Forms.Panel();
            this.grpIntOperands = new System.Windows.Forms.GroupBox();
            this.lblIntA = new System.Windows.Forms.Label();
            this.txtIntA = new System.Windows.Forms.TextBox();
            this.lblIntStatusA = new System.Windows.Forms.Label();
            this.lblIntB = new System.Windows.Forms.Label();
            this.txtIntB = new System.Windows.Forms.TextBox();
            this.lblIntStatusB = new System.Windows.Forms.Label();

            this.grpIntOps = new System.Windows.Forms.GroupBox();
            this.flpIntOps = new System.Windows.Forms.FlowLayoutPanel();
            this.btnIntAdd = new System.Windows.Forms.Button();
            this.btnIntSub = new System.Windows.Forms.Button();
            this.btnIntMul = new System.Windows.Forms.Button();
            this.btnIntDiv = new System.Windows.Forms.Button();
            this.btnIntMod = new System.Windows.Forms.Button();
            this.btnIntDivRem = new System.Windows.Forms.Button();
            this.btnIntNegA = new System.Windows.Forms.Button();
            this.btnIntAbsA = new System.Windows.Forms.Button();
            this.btnIntShl1 = new System.Windows.Forms.Button();
            this.btnIntShl8 = new System.Windows.Forms.Button();
            this.btnIntShr1 = new System.Windows.Forms.Button();
            this.btnIntShr8 = new System.Windows.Forms.Button();
            this.btnIntSwap = new System.Windows.Forms.Button();

            this.grpIntResult = new System.Windows.Forms.GroupBox();
            this.lblIntResDec = new System.Windows.Forms.Label();
            this.txtIntResDec = new System.Windows.Forms.TextBox();
            this.btnCopyIntDec = new System.Windows.Forms.Button();
            this.lblIntResHex = new System.Windows.Forms.Label();
            this.txtIntResHex = new System.Windows.Forms.TextBox();
            this.btnCopyIntHex = new System.Windows.Forms.Button();
            this.lblIntResBin = new System.Windows.Forms.Label();
            this.txtIntResBin = new System.Windows.Forms.TextBox();
            this.btnCopyIntBin = new System.Windows.Forms.Button();
            this.lblIntResRem = new System.Windows.Forms.Label();
            this.txtIntResRem = new System.Windows.Forms.TextBox();

            this.splitIntInspect = new System.Windows.Forms.SplitContainer();
            this.grpIntMeta = new System.Windows.Forms.GroupBox();
            this.flpIntInspectTarget = new System.Windows.Forms.FlowLayoutPanel();
            this.rbIntInspectRes = new System.Windows.Forms.RadioButton();
            this.rbIntInspectA = new System.Windows.Forms.RadioButton();
            this.rbIntInspectB = new System.Windows.Forms.RadioButton();
            this.rbIntInspectRem = new System.Windows.Forms.RadioButton();
            this.lblIntMetaSign = new System.Windows.Forms.Label();
            this.lblIntMetaBitLen = new System.Windows.Forms.Label();
            this.lblIntMetaLimbs = new System.Windows.Forms.Label();
            this.lblIntMetaTrailing = new System.Windows.Forms.Label();
            this.lblIntMetaBytes = new System.Windows.Forms.Label();
            this.lblIntMetaSize = new System.Windows.Forms.Label();

            this.grpIntLimbs = new System.Windows.Forms.GroupBox();
            this.dgvIntLimbs = new System.Windows.Forms.DataGridView();

            // ---------------------------------------------------------
            // ApFloat Tab Controls
            // ---------------------------------------------------------
            this.splitFloatMain = new System.Windows.Forms.SplitContainer();
            this.pnlFloatTop = new System.Windows.Forms.Panel();
            this.grpFloatConfig = new System.Windows.Forms.GroupBox();
            this.lblFloatPrec = new System.Windows.Forms.Label();
            this.numFloatPrec = new System.Windows.Forms.NumericUpDown();
            this.flpFloatPresets = new System.Windows.Forms.FlowLayoutPanel();
            this.btnPrecHalf = new System.Windows.Forms.Button();
            this.btnPrecSingle = new System.Windows.Forms.Button();
            this.btnPrecDouble = new System.Windows.Forms.Button();
            this.btnPrecQuad = new System.Windows.Forms.Button();
            this.btnPrecDefault = new System.Windows.Forms.Button();
            this.btnPrec1024 = new System.Windows.Forms.Button();
            this.lblFloatRound = new System.Windows.Forms.Label();
            this.cmbFloatRound = new System.Windows.Forms.ComboBox();
            this.lblFloatTarget = new System.Windows.Forms.Label();
            this.cmbFloatTarget = new System.Windows.Forms.ComboBox();

            this.grpFloatOperands = new System.Windows.Forms.GroupBox();
            this.lblFloatA = new System.Windows.Forms.Label();
            this.txtFloatA = new System.Windows.Forms.TextBox();
            this.lblFloatStatusA = new System.Windows.Forms.Label();
            this.lblFloatB = new System.Windows.Forms.Label();
            this.txtFloatB = new System.Windows.Forms.TextBox();
            this.lblFloatStatusB = new System.Windows.Forms.Label();

            this.grpFloatOps = new System.Windows.Forms.GroupBox();
            this.flpFloatOps = new System.Windows.Forms.FlowLayoutPanel();
            this.btnFloatAdd = new System.Windows.Forms.Button();
            this.btnFloatSub = new System.Windows.Forms.Button();
            this.btnFloatMul = new System.Windows.Forms.Button();
            this.btnFloatDiv = new System.Windows.Forms.Button();
            this.btnFloatNegA = new System.Windows.Forms.Button();
            this.btnFloatAbsA = new System.Windows.Forms.Button();
            this.btnFloatSwap = new System.Windows.Forms.Button();

            this.grpFloatResults = new System.Windows.Forms.GroupBox();
            this.lblFloatResDefault = new System.Windows.Forms.Label();
            this.txtFloatResDefault = new System.Windows.Forms.TextBox();
            this.btnCopyFloatDefault = new System.Windows.Forms.Button();
            this.lblFloatResR = new System.Windows.Forms.Label();
            this.txtFloatResR = new System.Windows.Forms.TextBox();
            this.btnCopyFloatR = new System.Windows.Forms.Button();
            this.lblFloatResF = new System.Windows.Forms.Label();
            this.txtFloatResF = new System.Windows.Forms.TextBox();
            this.btnCopyFloatF = new System.Windows.Forms.Button();
            this.lblFloatResHex = new System.Windows.Forms.Label();
            this.txtFloatResHex = new System.Windows.Forms.TextBox();
            this.btnCopyFloatHex = new System.Windows.Forms.Button();
            this.lblFloatResBin = new System.Windows.Forms.Label();
            this.txtFloatResBin = new System.Windows.Forms.TextBox();
            this.btnCopyFloatBin = new System.Windows.Forms.Button();
            this.lblFloatDiagnostics = new System.Windows.Forms.Label();

            this.splitFloatInspect = new System.Windows.Forms.SplitContainer();
            this.pnlFloatInspectLeft = new System.Windows.Forms.Panel();
            this.grpFloatInspectTarget = new System.Windows.Forms.GroupBox();
            this.flpFloatInspectTarget = new System.Windows.Forms.FlowLayoutPanel();
            this.rbFloatInspectRes = new System.Windows.Forms.RadioButton();
            this.rbFloatInspectA = new System.Windows.Forms.RadioButton();
            this.rbFloatInspectB = new System.Windows.Forms.RadioButton();

            this.grpFloatInternal = new System.Windows.Forms.GroupBox();
            this.lblFloatKind = new System.Windows.Forms.Label();
            this.lblFloatSign = new System.Windows.Forms.Label();
            this.lblFloatActualPrec = new System.Windows.Forms.Label();
            this.lblFloatExp = new System.Windows.Forms.Label();
            this.lblFloatMant = new System.Windows.Forms.Label();
            this.lblFloatMantBits = new System.Windows.Forms.Label();
            this.lblFloatTop = new System.Windows.Forms.Label();

            this.grpFloatIeee = new System.Windows.Forms.GroupBox();
            this.lblIeeeFormat = new System.Windows.Forms.Label();
            this.cmbIeeeInspectFormat = new System.Windows.Forms.ComboBox();
            this.lblIeeeSign = new System.Windows.Forms.Label();
            this.lblIeeeExp = new System.Windows.Forms.Label();
            this.lblIeeeFrac = new System.Windows.Forms.Label();
            this.lblIeeeClass = new System.Windows.Forms.Label();
            this.lblIeeeBytes = new System.Windows.Forms.Label();

            this.grpFloatLimbs = new System.Windows.Forms.GroupBox();
            this.dgvFloatLimbs = new System.Windows.Forms.DataGridView();

            ((System.ComponentModel.ISupportInitialize)(this.splitIntMain)).BeginInit();
            this.splitIntMain.Panel1.SuspendLayout();
            this.splitIntMain.Panel2.SuspendLayout();
            this.splitIntMain.SuspendLayout();
            this.pnlIntTop.SuspendLayout();
            this.grpIntOperands.SuspendLayout();
            this.grpIntOps.SuspendLayout();
            this.flpIntOps.SuspendLayout();
            this.grpIntResult.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitIntInspect)).BeginInit();
            this.splitIntInspect.Panel1.SuspendLayout();
            this.splitIntInspect.Panel2.SuspendLayout();
            this.splitIntInspect.SuspendLayout();
            this.grpIntMeta.SuspendLayout();
            this.flpIntInspectTarget.SuspendLayout();
            this.grpIntLimbs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvIntLimbs)).BeginInit();

            ((System.ComponentModel.ISupportInitialize)(this.splitFloatMain)).BeginInit();
            this.splitFloatMain.Panel1.SuspendLayout();
            this.splitFloatMain.Panel2.SuspendLayout();
            this.splitFloatMain.SuspendLayout();
            this.pnlFloatTop.SuspendLayout();
            this.grpFloatConfig.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFloatPrec)).BeginInit();
            this.flpFloatPresets.SuspendLayout();
            this.grpFloatOperands.SuspendLayout();
            this.grpFloatOps.SuspendLayout();
            this.flpFloatOps.SuspendLayout();
            this.grpFloatResults.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.splitFloatInspect)).BeginInit();
            this.splitFloatInspect.Panel1.SuspendLayout();
            this.splitFloatInspect.Panel2.SuspendLayout();
            this.splitFloatInspect.SuspendLayout();
            this.pnlFloatInspectLeft.SuspendLayout();
            this.grpFloatInspectTarget.SuspendLayout();
            this.flpFloatInspectTarget.SuspendLayout();
            this.grpFloatInternal.SuspendLayout();
            this.grpFloatIeee.SuspendLayout();
            this.grpFloatLimbs.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dgvFloatLimbs)).BeginInit();

            this.tabMain.SuspendLayout();
            this.tabApInt.SuspendLayout();
            this.tabApFloat.SuspendLayout();
            this.SuspendLayout();

            // ---------------------------------------------------------
            // tabMain
            // ---------------------------------------------------------
            this.tabMain.Controls.Add(this.tabApInt);
            this.tabMain.Controls.Add(this.tabApFloat);
            this.tabMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.tabMain.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.tabMain.Location = new System.Drawing.Point(0, 0);
            this.tabMain.Name = "tabMain";
            this.tabMain.SelectedIndex = 0;
            this.tabMain.Size = new System.Drawing.Size(1260, 840);
            this.tabMain.TabIndex = 0;

            // ---------------------------------------------------------
            // Tab 1: ApInt
            // ---------------------------------------------------------
            this.tabApInt.Controls.Add(this.splitIntMain);
            this.tabApInt.Location = new System.Drawing.Point(4, 25);
            this.tabApInt.Name = "tabApInt";
            this.tabApInt.Padding = new System.Windows.Forms.Padding(6);
            this.tabApInt.Size = new System.Drawing.Size(1252, 811);
            this.tabApInt.TabIndex = 0;
            this.tabApInt.Text = "  ApInt (Integer) Debugger  ";
            this.tabApInt.UseVisualStyleBackColor = true;

            // splitIntMain
            this.splitIntMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitIntMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitIntMain.Location = new System.Drawing.Point(6, 6);
            this.splitIntMain.Name = "splitIntMain";
            this.splitIntMain.Size = new System.Drawing.Size(1240, 799);
            this.splitIntMain.SplitterDistance = 470;
            this.splitIntMain.TabIndex = 0;

            // splitIntMain.Panel1: pnlIntTop
            this.splitIntMain.Panel1.Controls.Add(this.pnlIntTop);
            this.pnlIntTop.AutoScroll = true;
            this.pnlIntTop.Controls.Add(this.grpIntOperands);
            this.pnlIntTop.Controls.Add(this.grpIntOps);
            this.pnlIntTop.Controls.Add(this.grpIntResult);
            this.pnlIntTop.Dock = System.Windows.Forms.DockStyle.Fill;

            // grpIntOperands
            this.grpIntOperands.Controls.Add(this.lblIntA);
            this.grpIntOperands.Controls.Add(this.txtIntA);
            this.grpIntOperands.Controls.Add(this.lblIntStatusA);
            this.grpIntOperands.Controls.Add(this.lblIntB);
            this.grpIntOperands.Controls.Add(this.txtIntB);
            this.grpIntOperands.Controls.Add(this.lblIntStatusB);
            this.grpIntOperands.Location = new System.Drawing.Point(6, 6);
            this.grpIntOperands.Name = "grpIntOperands";
            this.grpIntOperands.Size = new System.Drawing.Size(1224, 130);
            this.grpIntOperands.TabIndex = 0;
            this.grpIntOperands.TabStop = false;
            this.grpIntOperands.Text = "Operands (Decimal, Hex 0x..., Binary 0b..., '_' digit separators supported)";

            this.lblIntA.AutoSize = true;
            this.lblIntA.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblIntA.Location = new System.Drawing.Point(12, 28);
            this.lblIntA.Name = "lblIntA";
            this.lblIntA.Size = new System.Drawing.Size(73, 15);
            this.lblIntA.Text = "Operand A:";

            this.txtIntA.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtIntA.Location = new System.Drawing.Point(92, 25);
            this.txtIntA.Name = "txtIntA";
            this.txtIntA.Size = new System.Drawing.Size(1115, 23);
            this.txtIntA.TabIndex = 0;
            this.txtIntA.Text = "12345678901234567890";

            this.lblIntStatusA.AutoSize = true;
            this.lblIntStatusA.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblIntStatusA.Location = new System.Drawing.Point(92, 51);
            this.lblIntStatusA.Name = "lblIntStatusA";
            this.lblIntStatusA.Size = new System.Drawing.Size(46, 17);
            this.lblIntStatusA.Text = "Ready";

            this.lblIntB.AutoSize = true;
            this.lblIntB.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblIntB.Location = new System.Drawing.Point(12, 77);
            this.lblIntB.Name = "lblIntB";
            this.lblIntB.Size = new System.Drawing.Size(72, 15);
            this.lblIntB.Text = "Operand B:";

            this.txtIntB.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtIntB.Location = new System.Drawing.Point(92, 74);
            this.txtIntB.Name = "txtIntB";
            this.txtIntB.Size = new System.Drawing.Size(1115, 23);
            this.txtIntB.TabIndex = 1;
            this.txtIntB.Text = "1000000007";

            this.lblIntStatusB.AutoSize = true;
            this.lblIntStatusB.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblIntStatusB.Location = new System.Drawing.Point(92, 100);
            this.lblIntStatusB.Name = "lblIntStatusB";
            this.lblIntStatusB.Size = new System.Drawing.Size(46, 17);
            this.lblIntStatusB.Text = "Ready";

            // grpIntOps
            this.grpIntOps.Controls.Add(this.flpIntOps);
            this.grpIntOps.Location = new System.Drawing.Point(6, 142);
            this.grpIntOps.Name = "grpIntOps";
            this.grpIntOps.Size = new System.Drawing.Size(1224, 68);
            this.grpIntOps.TabIndex = 1;
            this.grpIntOps.TabStop = false;
            this.grpIntOps.Text = "Operations";

            this.flpIntOps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpIntOps.Location = new System.Drawing.Point(3, 20);
            this.flpIntOps.Name = "flpIntOps";
            this.flpIntOps.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);
            this.flpIntOps.Size = new System.Drawing.Size(1218, 45);

            this.btnIntAdd.Text = "A + B";
            this.btnIntAdd.Size = new System.Drawing.Size(80, 32);
            this.btnIntSub.Text = "A - B";
            this.btnIntSub.Size = new System.Drawing.Size(80, 32);
            this.btnIntMul.Text = "A * B";
            this.btnIntMul.Size = new System.Drawing.Size(80, 32);
            this.btnIntDiv.Text = "A / B";
            this.btnIntDiv.Size = new System.Drawing.Size(80, 32);
            this.btnIntMod.Text = "A % B";
            this.btnIntMod.Size = new System.Drawing.Size(80, 32);
            this.btnIntDivRem.Text = "DivRem";
            this.btnIntDivRem.Size = new System.Drawing.Size(85, 32);
            this.btnIntNegA.Text = "-A";
            this.btnIntNegA.Size = new System.Drawing.Size(65, 32);
            this.btnIntAbsA.Text = "Abs(A)";
            this.btnIntAbsA.Size = new System.Drawing.Size(75, 32);
            this.btnIntShl1.Text = "A << 1";
            this.btnIntShl1.Size = new System.Drawing.Size(75, 32);
            this.btnIntShl8.Text = "A << 8";
            this.btnIntShl8.Size = new System.Drawing.Size(75, 32);
            this.btnIntShr1.Text = "A >> 1";
            this.btnIntShr1.Size = new System.Drawing.Size(75, 32);
            this.btnIntShr8.Text = "A >> 8";
            this.btnIntShr8.Size = new System.Drawing.Size(75, 32);
            this.btnIntSwap.Text = "Swap A ↔ B";
            this.btnIntSwap.Size = new System.Drawing.Size(100, 32);

            this.flpIntOps.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.btnIntAdd, this.btnIntSub, this.btnIntMul, this.btnIntDiv, this.btnIntMod, this.btnIntDivRem,
                this.btnIntNegA, this.btnIntAbsA, this.btnIntShl1, this.btnIntShl8, this.btnIntShr1, this.btnIntShr8, this.btnIntSwap
            });

            // grpIntResult
            this.grpIntResult.Controls.Add(this.lblIntResDec);
            this.grpIntResult.Controls.Add(this.txtIntResDec);
            this.grpIntResult.Controls.Add(this.btnCopyIntDec);
            this.grpIntResult.Controls.Add(this.lblIntResHex);
            this.grpIntResult.Controls.Add(this.txtIntResHex);
            this.grpIntResult.Controls.Add(this.btnCopyIntHex);
            this.grpIntResult.Controls.Add(this.lblIntResBin);
            this.grpIntResult.Controls.Add(this.txtIntResBin);
            this.grpIntResult.Controls.Add(this.btnCopyIntBin);
            this.grpIntResult.Controls.Add(this.lblIntResRem);
            this.grpIntResult.Controls.Add(this.txtIntResRem);
            this.grpIntResult.Location = new System.Drawing.Point(6, 216);
            this.grpIntResult.Name = "grpIntResult";
            this.grpIntResult.Size = new System.Drawing.Size(1224, 246);
            this.grpIntResult.TabIndex = 2;
            this.grpIntResult.TabStop = false;
            this.grpIntResult.Text = "Calculation Result (Live Formatted)";

            this.lblIntResDec.AutoSize = true;
            this.lblIntResDec.Location = new System.Drawing.Point(12, 26);
            this.lblIntResDec.Name = "lblIntResDec";
            this.lblIntResDec.Size = new System.Drawing.Size(56, 17);
            this.lblIntResDec.Text = "Decimal:";

            this.txtIntResDec.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtIntResDec.Location = new System.Drawing.Point(92, 23);
            this.txtIntResDec.Name = "txtIntResDec";
            this.txtIntResDec.ReadOnly = true;
            this.txtIntResDec.Size = new System.Drawing.Size(1030, 22);

            this.btnCopyIntDec.Text = "Copy";
            this.btnCopyIntDec.Location = new System.Drawing.Point(1130, 22);
            this.btnCopyIntDec.Size = new System.Drawing.Size(75, 25);

            this.lblIntResHex.AutoSize = true;
            this.lblIntResHex.Location = new System.Drawing.Point(12, 57);
            this.lblIntResHex.Name = "lblIntResHex";
            this.lblIntResHex.Size = new System.Drawing.Size(34, 17);
            this.lblIntResHex.Text = "Hex:";

            this.txtIntResHex.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtIntResHex.Location = new System.Drawing.Point(92, 54);
            this.txtIntResHex.Name = "txtIntResHex";
            this.txtIntResHex.ReadOnly = true;
            this.txtIntResHex.Size = new System.Drawing.Size(1030, 22);

            this.btnCopyIntHex.Text = "Copy";
            this.btnCopyIntHex.Location = new System.Drawing.Point(1130, 53);
            this.btnCopyIntHex.Size = new System.Drawing.Size(75, 25);

            this.lblIntResBin.AutoSize = true;
            this.lblIntResBin.Location = new System.Drawing.Point(12, 88);
            this.lblIntResBin.Name = "lblIntResBin";
            this.lblIntResBin.Size = new System.Drawing.Size(46, 17);
            this.lblIntResBin.Text = "Binary:";

            this.txtIntResBin.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtIntResBin.Location = new System.Drawing.Point(92, 85);
            this.txtIntResBin.Multiline = true;
            this.txtIntResBin.ScrollBars = System.Windows.Forms.ScrollBars.Vertical;
            this.txtIntResBin.Name = "txtIntResBin";
            this.txtIntResBin.ReadOnly = true;
            this.txtIntResBin.Size = new System.Drawing.Size(1030, 95);

            this.btnCopyIntBin.Text = "Copy";
            this.btnCopyIntBin.Location = new System.Drawing.Point(1130, 85);
            this.btnCopyIntBin.Size = new System.Drawing.Size(75, 25);

            this.lblIntResRem.AutoSize = true;
            this.lblIntResRem.Location = new System.Drawing.Point(12, 192);
            this.lblIntResRem.Name = "lblIntResRem";
            this.lblIntResRem.Size = new System.Drawing.Size(74, 17);
            this.lblIntResRem.Text = "Remainder:";

            this.txtIntResRem.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtIntResRem.Location = new System.Drawing.Point(92, 189);
            this.txtIntResRem.Name = "txtIntResRem";
            this.txtIntResRem.ReadOnly = true;
            this.txtIntResRem.Size = new System.Drawing.Size(1030, 22);

            // splitIntInspect (Panel 2)
            this.splitIntMain.Panel2.Controls.Add(this.splitIntInspect);
            this.splitIntInspect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitIntInspect.Location = new System.Drawing.Point(0, 0);
            this.splitIntInspect.Name = "splitIntInspect";
            this.splitIntInspect.Size = new System.Drawing.Size(1240, 325);
            this.splitIntInspect.SplitterDistance = 380;

            // grpIntMeta
            this.splitIntInspect.Panel1.Controls.Add(this.grpIntMeta);
            this.grpIntMeta.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpIntMeta.Location = new System.Drawing.Point(0, 0);
            this.grpIntMeta.Name = "grpIntMeta";
            this.grpIntMeta.Size = new System.Drawing.Size(380, 325);
            this.grpIntMeta.Text = "Deep State Inspector";

            this.flpIntInspectTarget.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.rbIntInspectRes, this.rbIntInspectA, this.rbIntInspectB, this.rbIntInspectRem
            });
            this.flpIntInspectTarget.Location = new System.Drawing.Point(10, 24);
            this.flpIntInspectTarget.Size = new System.Drawing.Size(360, 32);

            this.rbIntInspectRes.Text = "Result";
            this.rbIntInspectRes.Checked = true;
            this.rbIntInspectRes.AutoSize = true;
            this.rbIntInspectA.Text = "Op A";
            this.rbIntInspectA.AutoSize = true;
            this.rbIntInspectB.Text = "Op B";
            this.rbIntInspectB.AutoSize = true;
            this.rbIntInspectRem.Text = "Rem";
            this.rbIntInspectRem.AutoSize = true;

            this.lblIntMetaSign.Location = new System.Drawing.Point(12, 65);
            this.lblIntMetaSign.Size = new System.Drawing.Size(350, 22);
            this.lblIntMetaSign.Text = "Sign: Positive (+)";

            this.lblIntMetaBitLen.Location = new System.Drawing.Point(12, 95);
            this.lblIntMetaBitLen.Size = new System.Drawing.Size(350, 22);
            this.lblIntMetaBitLen.Text = "Bit Length: 0 bits";

            this.lblIntMetaLimbs.Location = new System.Drawing.Point(12, 125);
            this.lblIntMetaLimbs.Size = new System.Drawing.Size(350, 22);
            this.lblIntMetaLimbs.Text = "Limb Count (uint[ ]): 0 limbs";

            this.lblIntMetaTrailing.Location = new System.Drawing.Point(12, 155);
            this.lblIntMetaTrailing.Size = new System.Drawing.Size(350, 22);
            this.lblIntMetaTrailing.Text = "Trailing Zero Bits: 0";

            this.lblIntMetaBytes.Location = new System.Drawing.Point(12, 185);
            this.lblIntMetaBytes.Size = new System.Drawing.Size(350, 22);
            this.lblIntMetaBytes.Text = "Magnitude Byte Length: 0 bytes";

            this.lblIntMetaSize.Location = new System.Drawing.Point(12, 215);
            this.lblIntMetaSize.Size = new System.Drawing.Size(350, 22);
            this.lblIntMetaSize.Text = "Memory Footprint: ~0 bytes";

            this.grpIntMeta.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.flpIntInspectTarget, this.lblIntMetaSign, this.lblIntMetaBitLen,
                this.lblIntMetaLimbs, this.lblIntMetaTrailing, this.lblIntMetaBytes, this.lblIntMetaSize
            });

            // grpIntLimbs
            this.splitIntInspect.Panel2.Controls.Add(this.grpIntLimbs);
            this.grpIntLimbs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpIntLimbs.Text = "Raw 32-bit Limbs Table (Little-Endian, Least Significant Limb First)";

            this.dgvIntLimbs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvIntLimbs.ReadOnly = true;
            this.dgvIntLimbs.AllowUserToAddRows = false;
            this.dgvIntLimbs.AllowUserToDeleteRows = false;
            this.dgvIntLimbs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvIntLimbs.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.grpIntLimbs.Controls.Add(this.dgvIntLimbs);

            // ---------------------------------------------------------
            // Tab 2: ApFloat
            // ---------------------------------------------------------
            this.tabApFloat.Controls.Add(this.splitFloatMain);
            this.tabApFloat.Location = new System.Drawing.Point(4, 25);
            this.tabApFloat.Name = "tabApFloat";
            this.tabApFloat.Padding = new System.Windows.Forms.Padding(6);
            this.tabApFloat.Size = new System.Drawing.Size(1252, 811);
            this.tabApFloat.TabIndex = 1;
            this.tabApFloat.Text = "  ApFloat (Floating-Point) Debugger  ";
            this.tabApFloat.UseVisualStyleBackColor = true;

            // splitFloatMain
            this.splitFloatMain.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitFloatMain.Orientation = System.Windows.Forms.Orientation.Horizontal;
            this.splitFloatMain.Location = new System.Drawing.Point(6, 6);
            this.splitFloatMain.Size = new System.Drawing.Size(1240, 799);
            this.splitFloatMain.SplitterDistance = 470;

            // splitFloatMain.Panel1: pnlFloatTop
            this.splitFloatMain.Panel1.Controls.Add(this.pnlFloatTop);
            this.pnlFloatTop.AutoScroll = true;
            this.pnlFloatTop.Dock = System.Windows.Forms.DockStyle.Fill;

            // grpFloatConfig
            this.grpFloatConfig.Controls.Add(this.lblFloatPrec);
            this.grpFloatConfig.Controls.Add(this.numFloatPrec);
            this.grpFloatConfig.Controls.Add(this.flpFloatPresets);
            this.grpFloatConfig.Controls.Add(this.lblFloatRound);
            this.grpFloatConfig.Controls.Add(this.cmbFloatRound);
            this.grpFloatConfig.Controls.Add(this.lblFloatTarget);
            this.grpFloatConfig.Controls.Add(this.cmbFloatTarget);
            this.grpFloatConfig.Location = new System.Drawing.Point(6, 6);
            this.grpFloatConfig.Name = "grpFloatConfig";
            this.grpFloatConfig.Size = new System.Drawing.Size(1224, 65);
            this.grpFloatConfig.Text = "Precision, Rounding Mode & IEEE Target";

            this.lblFloatPrec.AutoSize = true;
            this.lblFloatPrec.Location = new System.Drawing.Point(10, 26);
            this.lblFloatPrec.Text = "Precision (bits):";

            this.numFloatPrec.Location = new System.Drawing.Point(105, 23);
            this.numFloatPrec.Maximum = new decimal(new int[] { 1048576, 0, 0, 0 });
            this.numFloatPrec.Minimum = new decimal(new int[] { 1, 0, 0, 0 });
            this.numFloatPrec.Value = new decimal(new int[] { 256, 0, 0, 0 });
            this.numFloatPrec.Size = new System.Drawing.Size(75, 24);

            this.flpFloatPresets.Location = new System.Drawing.Point(185, 21);
            this.flpFloatPresets.Size = new System.Drawing.Size(460, 32);

            this.btnPrecHalf.Text = "Half (11)";
            this.btnPrecHalf.Size = new System.Drawing.Size(70, 26);
            this.btnPrecSingle.Text = "Float (24)";
            this.btnPrecSingle.Size = new System.Drawing.Size(70, 26);
            this.btnPrecDouble.Text = "Double (53)";
            this.btnPrecDouble.Size = new System.Drawing.Size(80, 26);
            this.btnPrecQuad.Text = "Quad (113)";
            this.btnPrecQuad.Size = new System.Drawing.Size(80, 26);
            this.btnPrecDefault.Text = "256 (Default)";
            this.btnPrecDefault.Size = new System.Drawing.Size(90, 26);
            this.btnPrec1024.Text = "1024";
            this.btnPrec1024.Size = new System.Drawing.Size(55, 26);

            this.flpFloatPresets.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.btnPrecHalf, this.btnPrecSingle, this.btnPrecDouble, this.btnPrecQuad, this.btnPrecDefault, this.btnPrec1024
            });

            this.lblFloatRound.AutoSize = true;
            this.lblFloatRound.Location = new System.Drawing.Point(655, 26);
            this.lblFloatRound.Text = "Rounding:";

            this.cmbFloatRound.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFloatRound.Location = new System.Drawing.Point(725, 23);
            this.cmbFloatRound.Size = new System.Drawing.Size(150, 24);

            this.lblFloatTarget.AutoSize = true;
            this.lblFloatTarget.Location = new System.Drawing.Point(890, 26);
            this.lblFloatTarget.Text = "Format Mode:";

            this.cmbFloatTarget.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbFloatTarget.Location = new System.Drawing.Point(980, 23);
            this.cmbFloatTarget.Size = new System.Drawing.Size(230, 24);

            // grpFloatOperands
            this.grpFloatOperands.Controls.Add(this.lblFloatA);
            this.grpFloatOperands.Controls.Add(this.txtFloatA);
            this.grpFloatOperands.Controls.Add(this.lblFloatStatusA);
            this.grpFloatOperands.Controls.Add(this.lblFloatB);
            this.grpFloatOperands.Controls.Add(this.txtFloatB);
            this.grpFloatOperands.Controls.Add(this.lblFloatStatusB);
            this.grpFloatOperands.Location = new System.Drawing.Point(6, 75);
            this.grpFloatOperands.Name = "grpFloatOperands";
            this.grpFloatOperands.Size = new System.Drawing.Size(1224, 125);
            this.grpFloatOperands.Text = "Operands (Decimal, Scientific 1e..., C99 Hex Float 0x1.8p+1, Binary Float 0b..., NaN, ±Infinity, -0)";

            this.lblFloatA.AutoSize = true;
            this.lblFloatA.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblFloatA.Location = new System.Drawing.Point(12, 28);
            this.lblFloatA.Text = "Operand A:";

            this.txtFloatA.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatA.Location = new System.Drawing.Point(92, 25);
            this.txtFloatA.Size = new System.Drawing.Size(1115, 23);
            this.txtFloatA.Text = "0.1";

            this.lblFloatStatusA.AutoSize = true;
            this.lblFloatStatusA.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblFloatStatusA.Location = new System.Drawing.Point(92, 51);
            this.lblFloatStatusA.Text = "Ready";

            this.lblFloatB.AutoSize = true;
            this.lblFloatB.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
            this.lblFloatB.Location = new System.Drawing.Point(12, 75);
            this.lblFloatB.Text = "Operand B:";

            this.txtFloatB.Font = new System.Drawing.Font("Consolas", 10F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatB.Location = new System.Drawing.Point(92, 72);
            this.txtFloatB.Size = new System.Drawing.Size(1115, 23);
            this.txtFloatB.Text = "0.2";

            this.lblFloatStatusB.AutoSize = true;
            this.lblFloatStatusB.ForeColor = System.Drawing.Color.DarkGreen;
            this.lblFloatStatusB.Location = new System.Drawing.Point(92, 98);
            this.lblFloatStatusB.Text = "Ready";

            // grpFloatOps
            this.grpFloatOps.Controls.Add(this.flpFloatOps);
            this.grpFloatOps.Location = new System.Drawing.Point(6, 204);
            this.grpFloatOps.Name = "grpFloatOps";
            this.grpFloatOps.Size = new System.Drawing.Size(1224, 62);
            this.grpFloatOps.Text = "Operations";

            this.flpFloatOps.Dock = System.Windows.Forms.DockStyle.Fill;
            this.flpFloatOps.Location = new System.Drawing.Point(3, 20);
            this.flpFloatOps.Padding = new System.Windows.Forms.Padding(6, 2, 6, 2);

            this.btnFloatAdd.Text = "A + B";
            this.btnFloatAdd.Size = new System.Drawing.Size(90, 30);
            this.btnFloatSub.Text = "A - B";
            this.btnFloatSub.Size = new System.Drawing.Size(90, 30);
            this.btnFloatMul.Text = "A * B";
            this.btnFloatMul.Size = new System.Drawing.Size(90, 30);
            this.btnFloatDiv.Text = "A / B";
            this.btnFloatDiv.Size = new System.Drawing.Size(90, 30);
            this.btnFloatNegA.Text = "-A";
            this.btnFloatNegA.Size = new System.Drawing.Size(80, 30);
            this.btnFloatAbsA.Text = "Abs(A)";
            this.btnFloatAbsA.Size = new System.Drawing.Size(80, 30);
            this.btnFloatSwap.Text = "Swap A ↔ B";
            this.btnFloatSwap.Size = new System.Drawing.Size(100, 30);

            this.flpFloatOps.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.btnFloatAdd, this.btnFloatSub, this.btnFloatMul, this.btnFloatDiv,
                this.btnFloatNegA, this.btnFloatAbsA, this.btnFloatSwap
            });

            // grpFloatResults
            this.grpFloatResults.Controls.Add(this.lblFloatResDefault);
            this.grpFloatResults.Controls.Add(this.txtFloatResDefault);
            this.grpFloatResults.Controls.Add(this.btnCopyFloatDefault);
            this.grpFloatResults.Controls.Add(this.lblFloatResR);
            this.grpFloatResults.Controls.Add(this.txtFloatResR);
            this.grpFloatResults.Controls.Add(this.btnCopyFloatR);
            this.grpFloatResults.Controls.Add(this.lblFloatResF);
            this.grpFloatResults.Controls.Add(this.txtFloatResF);
            this.grpFloatResults.Controls.Add(this.btnCopyFloatF);
            this.grpFloatResults.Controls.Add(this.lblFloatResHex);
            this.grpFloatResults.Controls.Add(this.txtFloatResHex);
            this.grpFloatResults.Controls.Add(this.btnCopyFloatHex);
            this.grpFloatResults.Controls.Add(this.lblFloatResBin);
            this.grpFloatResults.Controls.Add(this.txtFloatResBin);
            this.grpFloatResults.Controls.Add(this.btnCopyFloatBin);
            this.grpFloatResults.Controls.Add(this.lblFloatDiagnostics);
            this.grpFloatResults.Location = new System.Drawing.Point(6, 270);
            this.grpFloatResults.Name = "grpFloatResults";
            this.grpFloatResults.Size = new System.Drawing.Size(1224, 195);
            this.grpFloatResults.Text = "Formatted Representations & Diagnostics";

            this.lblFloatResDefault.Location = new System.Drawing.Point(10, 22);
            this.lblFloatResDefault.Size = new System.Drawing.Size(140, 20);
            this.lblFloatResDefault.Text = "Default (Aligned):";

            this.txtFloatResDefault.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatResDefault.Location = new System.Drawing.Point(155, 20);
            this.txtFloatResDefault.ReadOnly = true;
            this.txtFloatResDefault.Size = new System.Drawing.Size(965, 22);

            this.btnCopyFloatDefault.Text = "Copy";
            this.btnCopyFloatDefault.Location = new System.Drawing.Point(1130, 19);
            this.btnCopyFloatDefault.Size = new System.Drawing.Size(75, 25);

            this.lblFloatResR.Location = new System.Drawing.Point(10, 50);
            this.lblFloatResR.Size = new System.Drawing.Size(140, 20);
            this.lblFloatResR.Text = "Shortest (\"R\"):";

            this.txtFloatResR.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatResR.Location = new System.Drawing.Point(155, 48);
            this.txtFloatResR.ReadOnly = true;
            this.txtFloatResR.Size = new System.Drawing.Size(965, 22);

            this.btnCopyFloatR.Text = "Copy";
            this.btnCopyFloatR.Location = new System.Drawing.Point(1130, 47);
            this.btnCopyFloatR.Size = new System.Drawing.Size(75, 25);

            this.lblFloatResF.Location = new System.Drawing.Point(10, 78);
            this.lblFloatResF.Size = new System.Drawing.Size(140, 20);
            this.lblFloatResF.Text = "Fixed (\"F6\"):";

            this.txtFloatResF.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatResF.Location = new System.Drawing.Point(155, 76);
            this.txtFloatResF.ReadOnly = true;
            this.txtFloatResF.Size = new System.Drawing.Size(965, 22);

            this.btnCopyFloatF.Text = "Copy";
            this.btnCopyFloatF.Location = new System.Drawing.Point(1130, 75);
            this.btnCopyFloatF.Size = new System.Drawing.Size(75, 25);

            this.lblFloatResHex.Location = new System.Drawing.Point(10, 106);
            this.lblFloatResHex.Size = new System.Drawing.Size(140, 20);
            this.lblFloatResHex.Text = "Hex Float (\"A\"):";

            this.txtFloatResHex.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatResHex.Location = new System.Drawing.Point(155, 104);
            this.txtFloatResHex.ReadOnly = true;
            this.txtFloatResHex.Size = new System.Drawing.Size(965, 22);

            this.btnCopyFloatHex.Text = "Copy";
            this.btnCopyFloatHex.Location = new System.Drawing.Point(1130, 103);
            this.btnCopyFloatHex.Size = new System.Drawing.Size(75, 25);

            this.lblFloatResBin.Location = new System.Drawing.Point(10, 134);
            this.lblFloatResBin.Size = new System.Drawing.Size(140, 20);
            this.lblFloatResBin.Text = "Binary Point (\"B\"):";

            this.txtFloatResBin.Font = new System.Drawing.Font("Consolas", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.txtFloatResBin.Location = new System.Drawing.Point(155, 132);
            this.txtFloatResBin.ReadOnly = true;
            this.txtFloatResBin.Size = new System.Drawing.Size(965, 22);

            this.btnCopyFloatBin.Text = "Copy";
            this.btnCopyFloatBin.Location = new System.Drawing.Point(1130, 131);
            this.btnCopyFloatBin.Size = new System.Drawing.Size(75, 25);

            this.lblFloatDiagnostics.AutoSize = true;
            this.lblFloatDiagnostics.ForeColor = System.Drawing.Color.DarkBlue;
            this.lblFloatDiagnostics.Location = new System.Drawing.Point(155, 163);
            this.lblFloatDiagnostics.Text = "Interval Engine: Idle";

            this.pnlFloatTop.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.grpFloatConfig, this.grpFloatOperands, this.grpFloatOps, this.grpFloatResults
            });

            // splitFloatInspect (Panel 2)
            this.splitFloatMain.Panel2.Controls.Add(this.splitFloatInspect);
            this.splitFloatInspect.Dock = System.Windows.Forms.DockStyle.Fill;
            this.splitFloatInspect.Location = new System.Drawing.Point(0, 0);
            this.splitFloatInspect.SplitterDistance = 450;

            // pnlFloatInspectLeft
            this.splitFloatInspect.Panel1.Controls.Add(this.pnlFloatInspectLeft);
            this.pnlFloatInspectLeft.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlFloatInspectLeft.AutoScroll = true;

            // grpFloatInspectTarget
            this.grpFloatInspectTarget.Controls.Add(this.flpFloatInspectTarget);
            this.grpFloatInspectTarget.Location = new System.Drawing.Point(6, 6);
            this.grpFloatInspectTarget.Size = new System.Drawing.Size(435, 52);
            this.grpFloatInspectTarget.Text = "Inspect Target";

            this.flpFloatInspectTarget.Dock = System.Windows.Forms.DockStyle.Fill;
            this.rbFloatInspectRes.Text = "Result";
            this.rbFloatInspectRes.Checked = true;
            this.rbFloatInspectRes.AutoSize = true;
            this.rbFloatInspectA.Text = "Op A";
            this.rbFloatInspectA.AutoSize = true;
            this.rbFloatInspectB.Text = "Op B";
            this.rbFloatInspectB.AutoSize = true;
            this.flpFloatInspectTarget.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.rbFloatInspectRes, this.rbFloatInspectA, this.rbFloatInspectB
            });

            // grpFloatInternal
            this.grpFloatInternal.Controls.Add(this.lblFloatKind);
            this.grpFloatInternal.Controls.Add(this.lblFloatSign);
            this.grpFloatInternal.Controls.Add(this.lblFloatActualPrec);
            this.grpFloatInternal.Controls.Add(this.lblFloatExp);
            this.grpFloatInternal.Controls.Add(this.lblFloatMant);
            this.grpFloatInternal.Controls.Add(this.lblFloatMantBits);
            this.grpFloatInternal.Controls.Add(this.lblFloatTop);
            this.grpFloatInternal.Location = new System.Drawing.Point(6, 62);
            this.grpFloatInternal.Size = new System.Drawing.Size(435, 145);
            this.grpFloatInternal.Text = "Mathematical Decomposition (±m × 2^e, m odd)";

            this.lblFloatKind.Location = new System.Drawing.Point(10, 22);
            this.lblFloatKind.Size = new System.Drawing.Size(200, 20);
            this.lblFloatKind.Text = "Kind: Finite";

            this.lblFloatSign.Location = new System.Drawing.Point(220, 22);
            this.lblFloatSign.Size = new System.Drawing.Size(200, 20);
            this.lblFloatSign.Text = "Sign: Positive (+)";

            this.lblFloatActualPrec.Location = new System.Drawing.Point(10, 45);
            this.lblFloatActualPrec.Size = new System.Drawing.Size(200, 20);
            this.lblFloatActualPrec.Text = "Precision: 256 bits";

            this.lblFloatExp.Location = new System.Drawing.Point(220, 45);
            this.lblFloatExp.Size = new System.Drawing.Size(200, 20);
            this.lblFloatExp.Text = "Exponent (e): 0";

            this.lblFloatMant.Location = new System.Drawing.Point(10, 68);
            this.lblFloatMant.Size = new System.Drawing.Size(415, 20);
            this.lblFloatMant.Text = "Significand (m): 0";

            this.lblFloatMantBits.Location = new System.Drawing.Point(10, 91);
            this.lblFloatMantBits.Size = new System.Drawing.Size(200, 20);
            this.lblFloatMantBits.Text = "m Bit Length: 0";

            this.lblFloatTop.Location = new System.Drawing.Point(220, 91);
            this.lblFloatTop.Size = new System.Drawing.Size(200, 20);
            this.lblFloatTop.Text = "Top Bit: 0";

            // grpFloatIeee
            this.grpFloatIeee.Controls.Add(this.lblIeeeFormat);
            this.grpFloatIeee.Controls.Add(this.cmbIeeeInspectFormat);
            this.grpFloatIeee.Controls.Add(this.lblIeeeSign);
            this.grpFloatIeee.Controls.Add(this.lblIeeeExp);
            this.grpFloatIeee.Controls.Add(this.lblIeeeFrac);
            this.grpFloatIeee.Controls.Add(this.lblIeeeClass);
            this.grpFloatIeee.Controls.Add(this.lblIeeeBytes);
            this.grpFloatIeee.Location = new System.Drawing.Point(6, 212);
            this.grpFloatIeee.Size = new System.Drawing.Size(435, 180);
            this.grpFloatIeee.Text = "IEEE 754 Interchange Bitfield Inspection";

            this.lblIeeeFormat.Location = new System.Drawing.Point(10, 24);
            this.lblIeeeFormat.Size = new System.Drawing.Size(100, 20);
            this.lblIeeeFormat.Text = "Format:";

            this.cmbIeeeInspectFormat.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cmbIeeeInspectFormat.Location = new System.Drawing.Point(115, 21);
            this.cmbIeeeInspectFormat.Size = new System.Drawing.Size(180, 24);

            this.lblIeeeSign.Location = new System.Drawing.Point(10, 52);
            this.lblIeeeSign.Size = new System.Drawing.Size(415, 20);
            this.lblIeeeSign.Text = "Sign bit: 0 (+)";

            this.lblIeeeExp.Location = new System.Drawing.Point(10, 75);
            this.lblIeeeExp.Size = new System.Drawing.Size(415, 20);
            this.lblIeeeExp.Text = "Biased Exponent: 0";

            this.lblIeeeFrac.Location = new System.Drawing.Point(10, 98);
            this.lblIeeeFrac.Size = new System.Drawing.Size(415, 20);
            this.lblIeeeFrac.Text = "Fraction field: 0";

            this.lblIeeeClass.Location = new System.Drawing.Point(10, 121);
            this.lblIeeeClass.Size = new System.Drawing.Size(415, 20);
            this.lblIeeeClass.Text = "Category: Normal";

            this.lblIeeeBytes.Location = new System.Drawing.Point(10, 144);
            this.lblIeeeBytes.Size = new System.Drawing.Size(415, 25);
            this.lblIeeeBytes.Text = "Bytes (LE): [00]";

            this.pnlFloatInspectLeft.Controls.AddRange(new System.Windows.Forms.Control[] {
                this.grpFloatInspectTarget, this.grpFloatInternal, this.grpFloatIeee
            });

            // grpFloatLimbs
            this.splitFloatInspect.Panel2.Controls.Add(this.grpFloatLimbs);
            this.grpFloatLimbs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.grpFloatLimbs.Text = "Significand Limbs (m uint[ ], Little-Endian)";

            this.dgvFloatLimbs.Dock = System.Windows.Forms.DockStyle.Fill;
            this.dgvFloatLimbs.ReadOnly = true;
            this.dgvFloatLimbs.AllowUserToAddRows = false;
            this.dgvFloatLimbs.AllowUserToDeleteRows = false;
            this.dgvFloatLimbs.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dgvFloatLimbs.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.grpFloatLimbs.Controls.Add(this.dgvFloatLimbs);

            // ---------------------------------------------------------
            // Form1
            // ---------------------------------------------------------
            this.AutoScaleDimensions = new System.Drawing.SizeF(7F, 15F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(1260, 840);
            this.Controls.Add(this.tabMain);
            this.MinimumSize = new System.Drawing.Size(1000, 700);
            this.Name = "Form1";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Natural — Arbitrary-Precision Debugging Playground";
            this.Load += new System.EventHandler(this.Form1_Load);

            this.splitIntMain.Panel1.ResumeLayout(false);
            this.splitIntMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitIntMain)).EndInit();
            this.splitIntMain.ResumeLayout(false);
            this.pnlIntTop.ResumeLayout(false);
            this.grpIntOperands.ResumeLayout(false);
            this.grpIntOperands.PerformLayout();
            this.grpIntOps.ResumeLayout(false);
            this.flpIntOps.ResumeLayout(false);
            this.grpIntResult.ResumeLayout(false);
            this.grpIntResult.PerformLayout();
            this.splitIntInspect.Panel1.ResumeLayout(false);
            this.splitIntInspect.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitIntInspect)).EndInit();
            this.splitIntInspect.ResumeLayout(false);
            this.grpIntMeta.ResumeLayout(false);
            this.flpIntInspectTarget.ResumeLayout(false);
            this.flpIntInspectTarget.PerformLayout();
            this.grpIntLimbs.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvIntLimbs)).EndInit();

            this.splitFloatMain.Panel1.ResumeLayout(false);
            this.splitFloatMain.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitFloatMain)).EndInit();
            this.splitFloatMain.ResumeLayout(false);
            this.pnlFloatTop.ResumeLayout(false);
            this.grpFloatConfig.ResumeLayout(false);
            this.grpFloatConfig.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.numFloatPrec)).EndInit();
            this.flpFloatPresets.ResumeLayout(false);
            this.grpFloatOperands.ResumeLayout(false);
            this.grpFloatOperands.PerformLayout();
            this.grpFloatOps.ResumeLayout(false);
            this.flpFloatOps.ResumeLayout(false);
            this.grpFloatResults.ResumeLayout(false);
            this.grpFloatResults.PerformLayout();
            this.splitFloatInspect.Panel1.ResumeLayout(false);
            this.splitFloatInspect.Panel2.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.splitFloatInspect)).EndInit();
            this.splitFloatInspect.ResumeLayout(false);
            this.pnlFloatInspectLeft.ResumeLayout(false);
            this.grpFloatInspectTarget.ResumeLayout(false);
            this.flpFloatInspectTarget.ResumeLayout(false);
            this.flpFloatInspectTarget.PerformLayout();
            this.grpFloatInternal.ResumeLayout(false);
            this.grpFloatIeee.ResumeLayout(false);
            this.grpFloatLimbs.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dgvFloatLimbs)).EndInit();

            this.tabMain.ResumeLayout(false);
            this.tabApInt.ResumeLayout(false);
            this.tabApFloat.ResumeLayout(false);
            this.ResumeLayout(false);
        }

        #endregion

        private System.Windows.Forms.TabControl tabMain;
        private System.Windows.Forms.TabPage tabApInt;
        private System.Windows.Forms.TabPage tabApFloat;

        // ApInt
        private System.Windows.Forms.SplitContainer splitIntMain;
        private System.Windows.Forms.Panel pnlIntTop;
        private System.Windows.Forms.GroupBox grpIntOperands;
        private System.Windows.Forms.Label lblIntA;
        private System.Windows.Forms.TextBox txtIntA;
        private System.Windows.Forms.Label lblIntStatusA;
        private System.Windows.Forms.Label lblIntB;
        private System.Windows.Forms.TextBox txtIntB;
        private System.Windows.Forms.Label lblIntStatusB;
        private System.Windows.Forms.GroupBox grpIntOps;
        private System.Windows.Forms.FlowLayoutPanel flpIntOps;
        private System.Windows.Forms.Button btnIntAdd;
        private System.Windows.Forms.Button btnIntSub;
        private System.Windows.Forms.Button btnIntMul;
        private System.Windows.Forms.Button btnIntDiv;
        private System.Windows.Forms.Button btnIntMod;
        private System.Windows.Forms.Button btnIntDivRem;
        private System.Windows.Forms.Button btnIntNegA;
        private System.Windows.Forms.Button btnIntAbsA;
        private System.Windows.Forms.Button btnIntShl1;
        private System.Windows.Forms.Button btnIntShl8;
        private System.Windows.Forms.Button btnIntShr1;
        private System.Windows.Forms.Button btnIntShr8;
        private System.Windows.Forms.Button btnIntSwap;
        private System.Windows.Forms.GroupBox grpIntResult;
        private System.Windows.Forms.Label lblIntResDec;
        private System.Windows.Forms.TextBox txtIntResDec;
        private System.Windows.Forms.Button btnCopyIntDec;
        private System.Windows.Forms.Label lblIntResHex;
        private System.Windows.Forms.TextBox txtIntResHex;
        private System.Windows.Forms.Button btnCopyIntHex;
        private System.Windows.Forms.Label lblIntResBin;
        private System.Windows.Forms.TextBox txtIntResBin;
        private System.Windows.Forms.Button btnCopyIntBin;
        private System.Windows.Forms.Label lblIntResRem;
        private System.Windows.Forms.TextBox txtIntResRem;

        private System.Windows.Forms.SplitContainer splitIntInspect;
        private System.Windows.Forms.GroupBox grpIntMeta;
        private System.Windows.Forms.FlowLayoutPanel flpIntInspectTarget;
        private System.Windows.Forms.RadioButton rbIntInspectRes;
        private System.Windows.Forms.RadioButton rbIntInspectA;
        private System.Windows.Forms.RadioButton rbIntInspectB;
        private System.Windows.Forms.RadioButton rbIntInspectRem;
        private System.Windows.Forms.Label lblIntMetaSign;
        private System.Windows.Forms.Label lblIntMetaBitLen;
        private System.Windows.Forms.Label lblIntMetaLimbs;
        private System.Windows.Forms.Label lblIntMetaTrailing;
        private System.Windows.Forms.Label lblIntMetaBytes;
        private System.Windows.Forms.Label lblIntMetaSize;
        private System.Windows.Forms.GroupBox grpIntLimbs;
        private System.Windows.Forms.DataGridView dgvIntLimbs;

        // ApFloat
        private System.Windows.Forms.SplitContainer splitFloatMain;
        private System.Windows.Forms.Panel pnlFloatTop;
        private System.Windows.Forms.GroupBox grpFloatConfig;
        private System.Windows.Forms.Label lblFloatPrec;
        private System.Windows.Forms.NumericUpDown numFloatPrec;
        private System.Windows.Forms.FlowLayoutPanel flpFloatPresets;
        private System.Windows.Forms.Button btnPrecHalf;
        private System.Windows.Forms.Button btnPrecSingle;
        private System.Windows.Forms.Button btnPrecDouble;
        private System.Windows.Forms.Button btnPrecQuad;
        private System.Windows.Forms.Button btnPrecDefault;
        private System.Windows.Forms.Button btnPrec1024;
        private System.Windows.Forms.Label lblFloatRound;
        private System.Windows.Forms.ComboBox cmbFloatRound;
        private System.Windows.Forms.Label lblFloatTarget;
        private System.Windows.Forms.ComboBox cmbFloatTarget;
        private System.Windows.Forms.GroupBox grpFloatOperands;
        private System.Windows.Forms.Label lblFloatA;
        private System.Windows.Forms.TextBox txtFloatA;
        private System.Windows.Forms.Label lblFloatStatusA;
        private System.Windows.Forms.Label lblFloatB;
        private System.Windows.Forms.TextBox txtFloatB;
        private System.Windows.Forms.Label lblFloatStatusB;
        private System.Windows.Forms.GroupBox grpFloatOps;
        private System.Windows.Forms.FlowLayoutPanel flpFloatOps;
        private System.Windows.Forms.Button btnFloatAdd;
        private System.Windows.Forms.Button btnFloatSub;
        private System.Windows.Forms.Button btnFloatMul;
        private System.Windows.Forms.Button btnFloatDiv;
        private System.Windows.Forms.Button btnFloatNegA;
        private System.Windows.Forms.Button btnFloatAbsA;
        private System.Windows.Forms.Button btnFloatSwap;
        private System.Windows.Forms.GroupBox grpFloatResults;
        private System.Windows.Forms.Label lblFloatResDefault;
        private System.Windows.Forms.TextBox txtFloatResDefault;
        private System.Windows.Forms.Button btnCopyFloatDefault;
        private System.Windows.Forms.Label lblFloatResR;
        private System.Windows.Forms.TextBox txtFloatResR;
        private System.Windows.Forms.Button btnCopyFloatR;
        private System.Windows.Forms.Label lblFloatResF;
        private System.Windows.Forms.TextBox txtFloatResF;
        private System.Windows.Forms.Button btnCopyFloatF;
        private System.Windows.Forms.Label lblFloatResHex;
        private System.Windows.Forms.TextBox txtFloatResHex;
        private System.Windows.Forms.Button btnCopyFloatHex;
        private System.Windows.Forms.Label lblFloatResBin;
        private System.Windows.Forms.TextBox txtFloatResBin;
        private System.Windows.Forms.Button btnCopyFloatBin;
        private System.Windows.Forms.Label lblFloatDiagnostics;

        private System.Windows.Forms.SplitContainer splitFloatInspect;
        private System.Windows.Forms.Panel pnlFloatInspectLeft;
        private System.Windows.Forms.GroupBox grpFloatInspectTarget;
        private System.Windows.Forms.FlowLayoutPanel flpFloatInspectTarget;
        private System.Windows.Forms.RadioButton rbFloatInspectRes;
        private System.Windows.Forms.RadioButton rbFloatInspectA;
        private System.Windows.Forms.RadioButton rbFloatInspectB;
        private System.Windows.Forms.GroupBox grpFloatInternal;
        private System.Windows.Forms.Label lblFloatKind;
        private System.Windows.Forms.Label lblFloatSign;
        private System.Windows.Forms.Label lblFloatActualPrec;
        private System.Windows.Forms.Label lblFloatExp;
        private System.Windows.Forms.Label lblFloatMant;
        private System.Windows.Forms.Label lblFloatMantBits;
        private System.Windows.Forms.Label lblFloatTop;
        private System.Windows.Forms.GroupBox grpFloatIeee;
        private System.Windows.Forms.Label lblIeeeFormat;
        private System.Windows.Forms.ComboBox cmbIeeeInspectFormat;
        private System.Windows.Forms.Label lblIeeeSign;
        private System.Windows.Forms.Label lblIeeeExp;
        private System.Windows.Forms.Label lblIeeeFrac;
        private System.Windows.Forms.Label lblIeeeClass;
        private System.Windows.Forms.Label lblIeeeBytes;
        private System.Windows.Forms.GroupBox grpFloatLimbs;
        private System.Windows.Forms.DataGridView dgvFloatLimbs;
    }
}
