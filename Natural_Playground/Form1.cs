using System.Diagnostics;
using System.Globalization;
using System.Text;
using Natural;

namespace Natural_Playground
{
    public partial class Form1 : Form
    {
        // -------------------------------------------------------------
        // ApInt State
        // -------------------------------------------------------------
        private ApInt _intA;
        private ApInt _intB;
        private ApInt? _intResult;
        private ApInt? _intRemainder;
        private string _lastIntOp = "+";
        private bool _intAValid;
        private bool _intBValid;

        // -------------------------------------------------------------
        // ApFloat State
        // -------------------------------------------------------------
        private ApFloat _floatA;
        private ApFloat _floatB;
        private ApFloat? _floatResult;
        private string _lastFloatOp = "+";
        private bool _floatAValid;
        private bool _floatBValid;

        private Func<ApFloat> _boundFloatProvider;
        private System.Windows.Forms.Timer _bindTimer;

        public void BindToTotalDP(Func<ApFloat> provider)
        {
            _boundFloatProvider = provider;
            txtFloatA.ReadOnly = true; // prevent manual edits while bound
            _bindTimer = new System.Windows.Forms.Timer();
            _bindTimer.Interval = 200; // updates 5 times a second
            _bindTimer.Tick += (s, e) => {
                string val = _boundFloatProvider().ToString("R", CultureInfo.InvariantCulture);
                if (txtFloatA.Text != val)
                    txtFloatA.Text = val;
            };
            _bindTimer.Start();
        }

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            tabMain.TabPages.Remove(tabApInt);
            tabMain.SelectedTab = tabApFloat;

            SetupGridColumns(dgvIntLimbs);
            SetupGridColumns(dgvFloatLimbs);

            // Populate ApFloat Rounding Modes
            cmbFloatRound.Items.AddRange(new object[] {
                RoundingMode.ToNearestEven,
                RoundingMode.TowardZero,
                RoundingMode.TowardPositive,
                RoundingMode.TowardNegative
            });
            cmbFloatRound.SelectedItem = RoundingMode.ToNearestEven;

            // Populate ApFloat Target Formats
            cmbFloatTarget.Items.AddRange(new object[] {
                "Unbounded Exponent (Native ApFloat)",
                "Binary16 (Half: 5 exp, 11 prec)",
                "Binary32 (Single: 8 exp, 24 prec)",
                "Binary64 (Double: 11 exp, 53 prec)",
                "Binary128 (Quad: 15 exp, 113 prec)",
                "Binary256 (19 exp, 237 prec)"
            });
            cmbFloatTarget.SelectedIndex = 0;

            // Populate IEEE Inspection Format
            cmbIeeeInspectFormat.Items.AddRange(new object[] {
                "Binary16 (Half - 16 bit)",
                "Binary32 (Single - 32 bit)",
                "Binary64 (Double - 64 bit)",
                "Binary128 (Quad - 128 bit)",
                "Binary256 (256 bit)"
            });
            cmbIeeeInspectFormat.SelectedIndex = 2; // Default to Binary64 / Double

            // Wire ApInt events
            txtIntA.TextChanged += (s, ev) => { UpdateIntInputs(); RecalculateInt(); };
            txtIntB.TextChanged += (s, ev) => { UpdateIntInputs(); RecalculateInt(); };

            btnIntAdd.Click += (s, ev) => { _lastIntOp = "+"; RecalculateInt(); };
            btnIntSub.Click += (s, ev) => { _lastIntOp = "-"; RecalculateInt(); };
            btnIntMul.Click += (s, ev) => { _lastIntOp = "*"; RecalculateInt(); };
            btnIntDiv.Click += (s, ev) => { _lastIntOp = "/"; RecalculateInt(); };
            btnIntMod.Click += (s, ev) => { _lastIntOp = "%"; RecalculateInt(); };
            btnIntDivRem.Click += (s, ev) => { _lastIntOp = "DivRem"; RecalculateInt(); };

            btnIntNegA.Click += (s, ev) => {
                if (_intAValid) {
                    txtIntA.Text = (-_intA).ToString();
                }
            };
            btnIntAbsA.Click += (s, ev) => {
                if (_intAValid) {
                    txtIntA.Text = ApInt.Abs(_intA).ToString();
                }
            };
            btnIntShl1.Click += (s, ev) => {
                if (_intAValid) {
                    txtIntA.Text = (_intA << 1).ToString();
                }
            };
            btnIntShl8.Click += (s, ev) => {
                if (_intAValid) {
                    txtIntA.Text = (_intA << 8).ToString();
                }
            };
            btnIntShr1.Click += (s, ev) => {
                if (_intAValid) {
                    txtIntA.Text = (_intA >> 1).ToString();
                }
            };
            btnIntShr8.Click += (s, ev) => {
                if (_intAValid) {
                    txtIntA.Text = (_intA >> 8).ToString();
                }
            };
            btnIntSwap.Click += (s, ev) => {
                string temp = txtIntA.Text;
                txtIntA.Text = txtIntB.Text;
                txtIntB.Text = temp;
            };

            btnCopyIntDec.Click += (s, ev) => CopyToClipboard(txtIntResDec.Text);
            btnCopyIntHex.Click += (s, ev) => CopyToClipboard(txtIntResHex.Text);
            btnCopyIntBin.Click += (s, ev) => CopyToClipboard(txtIntResBin.Text);

            rbIntInspectRes.CheckedChanged += (s, ev) => { if (rbIntInspectRes.Checked) RefreshIntInspection(); };
            rbIntInspectA.CheckedChanged += (s, ev) => { if (rbIntInspectA.Checked) RefreshIntInspection(); };
            rbIntInspectB.CheckedChanged += (s, ev) => { if (rbIntInspectB.Checked) RefreshIntInspection(); };
            rbIntInspectRem.CheckedChanged += (s, ev) => { if (rbIntInspectRem.Checked) RefreshIntInspection(); };

            // Wire ApFloat events
            txtFloatA.TextChanged += (s, ev) => { UpdateFloatInputs(); RecalculateFloat(); };
            txtFloatB.TextChanged += (s, ev) => { UpdateFloatInputs(); RecalculateFloat(); };
            numFloatPrec.ValueChanged += (s, ev) => { UpdateFloatInputs(); RecalculateFloat(); };
            cmbFloatRound.SelectedIndexChanged += (s, ev) => { UpdateFloatInputs(); RecalculateFloat(); };
            cmbFloatTarget.SelectedIndexChanged += (s, ev) => { RecalculateFloat(); };
            cmbIeeeInspectFormat.SelectedIndexChanged += (s, ev) => RefreshFloatInspection();

            btnPrecHalf.Click += (s, ev) => numFloatPrec.Value = 11;
            btnPrecSingle.Click += (s, ev) => numFloatPrec.Value = 24;
            btnPrecDouble.Click += (s, ev) => numFloatPrec.Value = 53;
            btnPrecQuad.Click += (s, ev) => numFloatPrec.Value = 113;
            btnPrecDefault.Click += (s, ev) => numFloatPrec.Value = 256;
            btnPrec1024.Click += (s, ev) => numFloatPrec.Value = 1024;

            btnFloatAdd.Click += (s, ev) => { _lastFloatOp = "+"; RecalculateFloat(); };
            btnFloatSub.Click += (s, ev) => { _lastFloatOp = "-"; RecalculateFloat(); };
            btnFloatMul.Click += (s, ev) => { _lastFloatOp = "*"; RecalculateFloat(); };
            btnFloatDiv.Click += (s, ev) => { _lastFloatOp = "/"; RecalculateFloat(); };

            btnFloatNegA.Click += (s, ev) => {
                if (_floatAValid) {
                    txtFloatA.Text = (-_floatA).ToString();
                }
            };
            btnFloatAbsA.Click += (s, ev) => {
                if (_floatAValid) {
                    txtFloatA.Text = ApFloat.Abs(_floatA).ToString();
                }
            };
            btnFloatSwap.Click += (s, ev) => {
                string temp = txtFloatA.Text;
                txtFloatA.Text = txtFloatB.Text;
                txtFloatB.Text = temp;
            };

            btnCopyFloatDefault.Click += (s, ev) => CopyToClipboard(txtFloatResDefault.Text);
            btnCopyFloatR.Click += (s, ev) => CopyToClipboard(txtFloatResR.Text);
            btnCopyFloatF.Click += (s, ev) => CopyToClipboard(txtFloatResF.Text);
            btnCopyFloatHex.Click += (s, ev) => CopyToClipboard(txtFloatResHex.Text);
            btnCopyFloatBin.Click += (s, ev) => CopyToClipboard(txtFloatResBin.Text);

            rbFloatInspectRes.CheckedChanged += (s, ev) => { if (rbFloatInspectRes.Checked) RefreshFloatInspection(); };
            rbFloatInspectA.CheckedChanged += (s, ev) => { if (rbFloatInspectA.Checked) RefreshFloatInspection(); };
            rbFloatInspectB.CheckedChanged += (s, ev) => { if (rbFloatInspectB.Checked) RefreshFloatInspection(); };

            // Initial calculations
            UpdateIntInputs();
            RecalculateInt();

            UpdateFloatInputs();
            RecalculateFloat();
        }

        private static void SetupGridColumns(DataGridView dgv)
        {
            dgv.Columns.Clear();
            dgv.Columns.Add("colIdx", "Limb Index");
            dgv.Columns.Add("colDec", "Value (UInt32)");
            dgv.Columns.Add("colHex", "Hexadecimal");
            dgv.Columns.Add("colBin", "32-bit Binary (Nibble Formatted)");

            dgv.Columns[0].Width = 140;
            dgv.Columns[1].Width = 150;
            dgv.Columns[2].Width = 150;
            dgv.Columns[3].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            dgv.RowHeadersVisible = false;
            dgv.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        }

        // =============================================================
        // ApInt Calculation & Inspection
        // =============================================================

        private void UpdateIntInputs()
        {
            _intAValid = ApInt.TryParse(txtIntA.Text, out _intA);
            if (_intAValid)
            {
                lblIntStatusA.ForeColor = Color.DarkGreen;
                lblIntStatusA.Text = $"Valid — {_intA.BitLength} bits, {_intA.Limbs.Length} limb(s)";
            }
            else
            {
                lblIntStatusA.ForeColor = Color.DarkRed;
                lblIntStatusA.Text = "Invalid integer format (supports Decimal, 0x Hex, 0b Binary, '_')";
            }

            _intBValid = ApInt.TryParse(txtIntB.Text, out _intB);
            if (_intBValid)
            {
                lblIntStatusB.ForeColor = Color.DarkGreen;
                lblIntStatusB.Text = $"Valid — {_intB.BitLength} bits, {_intB.Limbs.Length} limb(s)";
            }
            else
            {
                lblIntStatusB.ForeColor = Color.DarkRed;
                lblIntStatusB.Text = "Invalid integer format (supports Decimal, 0x Hex, 0b Binary, '_')";
            }
        }

        private void RecalculateInt()
        {
            if (!_intAValid || !_intBValid)
            {
                txtIntResDec.Text = "(Waiting for valid operands...)";
                txtIntResHex.Text = "";
                txtIntResBin.Text = "";
                txtIntResRem.Text = "";
                _intResult = null;
                _intRemainder = null;
                RefreshIntInspection();
                return;
            }

            try
            {
                switch (_lastIntOp)
                {
                    case "+":
                        _intResult = _intA + _intB;
                        _intRemainder = null;
                        break;
                    case "-":
                        _intResult = _intA - _intB;
                        _intRemainder = null;
                        break;
                    case "*":
                        _intResult = _intA * _intB;
                        _intRemainder = null;
                        break;
                    case "/":
                        _intResult = _intA / _intB;
                        _intRemainder = null;
                        break;
                    case "%":
                        _intResult = _intA % _intB;
                        _intRemainder = null;
                        break;
                    case "DivRem":
                        var (q, r) = ApInt.DivRem(_intA, _intB);
                        _intResult = q;
                        _intRemainder = r;
                        break;
                    default:
                        _intResult = _intA + _intB;
                        _intRemainder = null;
                        break;
                }

                if (_intResult.HasValue)
                {
                    txtIntResDec.Text = _intResult.Value.ToString("D");
                    txtIntResHex.Text = (_intResult.Value.IsNegative ? "-" : "") + "0x" + _intResult.Value.ToString("X");
                    txtIntResBin.Text = (_intResult.Value.IsNegative ? "-" : "") + "0b" + _intResult.Value.ToString("B");
                }

                if (_intRemainder.HasValue)
                {
                    txtIntResRem.Text = _intRemainder.Value.ToString("D");
                }
                else
                {
                    txtIntResRem.Text = "N/A (DivRem not selected)";
                }
            }
            catch (DivideByZeroException)
            {
                txtIntResDec.Text = "Error: Division by zero!";
                txtIntResHex.Text = "Error: Division by zero!";
                txtIntResBin.Text = "Error: Division by zero!";
                txtIntResRem.Text = "";
                _intResult = null;
                _intRemainder = null;
            }
            catch (Exception ex)
            {
                txtIntResDec.Text = $"Error: {ex.Message}";
                txtIntResHex.Text = "";
                txtIntResBin.Text = "";
                txtIntResRem.Text = "";
                _intResult = null;
                _intRemainder = null;
            }

            RefreshIntInspection();
        }

        private void RefreshIntInspection()
        {
            ApInt? inspect = null;
            string targetName = "Result";

            if (rbIntInspectRes.Checked)
            {
                inspect = _intResult;
                targetName = "Result";
            }
            else if (rbIntInspectA.Checked)
            {
                inspect = _intAValid ? _intA : null;
                targetName = "Operand A";
            }
            else if (rbIntInspectB.Checked)
            {
                inspect = _intBValid ? _intB : null;
                targetName = "Operand B";
            }
            else if (rbIntInspectRem.Checked)
            {
                inspect = _intRemainder;
                targetName = "Remainder";
            }

            if (!inspect.HasValue)
            {
                lblIntMetaSign.Text = "Sign: —";
                lblIntMetaBitLen.Text = "Bit Length: —";
                lblIntMetaLimbs.Text = "Limb Count: —";
                lblIntMetaTrailing.Text = "Trailing Zero Bits: —";
                lblIntMetaBytes.Text = "Magnitude Byte Length: —";
                lblIntMetaSize.Text = "Memory Footprint: —";
                dgvIntLimbs.Rows.Clear();
                return;
            }

            ApInt val = inspect.Value;
            uint[] limbs = val.Limbs;
            long bitLen = val.BitLength;
            long trailingZeros = val.IsZero ? 0 : Magnitude.TrailingZeroCount(limbs);
            byte[] bytes = val.ToMagnitudeBytes();

            lblIntMetaSign.Text = $"Sign: {(val.IsZero ? "Zero (0)" : val.IsNegative ? "Negative (-)" : "Positive (+)")}";
            lblIntMetaBitLen.Text = $"Bit Length: {bitLen:N0} bit(s)";
            lblIntMetaLimbs.Text = $"Limb Count (uint[ ]): {limbs.Length} limb(s)";
            lblIntMetaTrailing.Text = $"Trailing Zero Bits: {trailingZeros:N0}";
            lblIntMetaBytes.Text = $"Magnitude Byte Length: {bytes.Length} bytes";
            lblIntMetaSize.Text = $"Memory Footprint: ~{24 + limbs.Length * 4} bytes ({targetName})";

            dgvIntLimbs.Rows.Clear();
            if (limbs.Length == 0)
            {
                dgvIntLimbs.Rows.Add("[Empty / Zero]", "0", "0x00000000", "0000 0000 0000 0000 0000 0000 0000 0000");
            }
            else
            {
                for (int i = 0; i < limbs.Length; i++)
                {
                    uint limb = limbs[i];
                    string tag = i == 0 ? " [0] (LSB)" : i == limbs.Length - 1 ? $" [{i}] (MSB)" : $" [{i}]";
                    dgvIntLimbs.Rows.Add(
                        tag,
                        limb.ToString("N0", CultureInfo.InvariantCulture),
                        $"0x{limb:X8}",
                        FormatBinaryNibbles(limb)
                    );
                }
            }
        }

        // =============================================================
        // ApFloat Calculation & Inspection
        // =============================================================

        private void UpdateFloatInputs()
        {
            int precision = (int)numFloatPrec.Value;
            RoundingMode mode = (RoundingMode)(cmbFloatRound.SelectedItem ?? RoundingMode.ToNearestEven);

            _floatAValid = ApFloat.TryParse(txtFloatA.Text, precision, mode, CultureInfo.InvariantCulture, out _floatA);
            if (_floatAValid)
            {
                lblFloatStatusA.ForeColor = Color.DarkGreen;
                lblFloatStatusA.Text = $"Valid — {_floatA.KindName}, Precision {_floatA.Precision} bits";
            }
            else
            {
                lblFloatStatusA.ForeColor = Color.DarkRed;
                lblFloatStatusA.Text = "Invalid float format (decimal, 1eN, 0x1.8p1, 0b1.01p1, NaN, ±Infinity)";
            }

            _floatBValid = ApFloat.TryParse(txtFloatB.Text, precision, mode, CultureInfo.InvariantCulture, out _floatB);
            if (_floatBValid)
            {
                lblFloatStatusB.ForeColor = Color.DarkGreen;
                lblFloatStatusB.Text = $"Valid — {_floatB.KindName}, Precision {_floatB.Precision} bits";
            }
            else
            {
                lblFloatStatusB.ForeColor = Color.DarkRed;
                lblFloatStatusB.Text = "Invalid float format (decimal, 1eN, 0x1.8p1, 0b1.01p1, NaN, ±Infinity)";
            }
        }

        private IeeeFormat? GetSelectedTargetFormat()
        {
            return cmbFloatTarget.SelectedIndex switch
            {
                1 => IeeeFormat.Binary16,
                2 => IeeeFormat.Binary32,
                3 => IeeeFormat.Binary64,
                4 => IeeeFormat.Binary128,
                5 => IeeeFormat.Binary256,
                _ => null,
            };
        }

        private void RecalculateFloat()
        {
            if (!_floatAValid || !_floatBValid)
            {
                txtFloatResDefault.Text = "(Waiting for valid operands...)";
                txtFloatResR.Text = "";
                txtFloatResF.Text = "";
                txtFloatResHex.Text = "";
                txtFloatResBin.Text = "";
                lblFloatDiagnostics.Text = "Interval Engine: Idle";
                _floatResult = null;
                RefreshFloatInspection();
                return;
            }

            int precision = (int)numFloatPrec.Value;
            RoundingMode mode = (RoundingMode)(cmbFloatRound.SelectedItem ?? RoundingMode.ToNearestEven);
            IeeeFormat? targetFormat = GetSelectedTargetFormat();

            var sw = Stopwatch.StartNew();
            try
            {
                if (targetFormat.HasValue)
                {
                    IeeeFormat fmt = targetFormat.Value;
                    _floatResult = _lastFloatOp switch
                    {
                        "+" => ApFloat.Add(_floatA, _floatB, fmt, mode),
                        "-" => ApFloat.Subtract(_floatA, _floatB, fmt, mode),
                        "*" => ApFloat.Multiply(_floatA, _floatB, fmt, mode),
                        "/" => ApFloat.Divide(_floatA, _floatB, fmt, mode),
                        _ => ApFloat.Add(_floatA, _floatB, fmt, mode),
                    };
                }
                else
                {
                    _floatResult = _lastFloatOp switch
                    {
                        "+" => ApFloat.Add(_floatA, _floatB, precision, mode),
                        "-" => ApFloat.Subtract(_floatA, _floatB, precision, mode),
                        "*" => ApFloat.Multiply(_floatA, _floatB, precision, mode),
                        "/" => ApFloat.Divide(_floatA, _floatB, precision, mode),
                        _ => ApFloat.Add(_floatA, _floatB, precision, mode),
                    };
                }
                sw.Stop();

                int rounds = ApFloat.LastRounds;
                bool wasExact = ApFloat.LastExact;
                double elapsedUs = sw.Elapsed.TotalMicroseconds;

                lblFloatDiagnostics.Text = targetFormat.HasValue
                    ? $"Format Target: {targetFormat.Value} | Time: {elapsedUs:F1} µs"
                    : $"Interval Engine: {(wasExact ? "Exact Route" : rounds > 0 ? $"Certified via Bounds ({rounds} round{(rounds > 1 ? "s" : "")})" : "Standard Path")} | Time: {elapsedUs:F1} µs";

                if (_floatResult.HasValue)
                {
                    ApFloat res = _floatResult.Value;
                    txtFloatResDefault.Text = res.ToString();
                    txtFloatResR.Text = res.ToString("R", CultureInfo.InvariantCulture);
                    txtFloatResF.Text = res.ToString("F6", CultureInfo.InvariantCulture);
                    txtFloatResHex.Text = res.ToString("A", CultureInfo.InvariantCulture);
                    txtFloatResBin.Text = res.ToString("B", CultureInfo.InvariantCulture);
                }
            }
            catch (Exception ex)
            {
                sw.Stop();
                txtFloatResDefault.Text = $"Error: {ex.Message}";
                txtFloatResR.Text = "";
                txtFloatResF.Text = "";
                txtFloatResHex.Text = "";
                txtFloatResBin.Text = "";
                lblFloatDiagnostics.Text = $"Failed: {ex.Message}";
                _floatResult = null;
            }

            RefreshFloatInspection();
        }

        private void RefreshFloatInspection()
        {
            ApFloat? inspect = null;
            if (rbFloatInspectRes.Checked)
            {
                inspect = _floatResult;
            }
            else if (rbFloatInspectA.Checked)
            {
                inspect = _floatAValid ? _floatA : null;
            }
            else if (rbFloatInspectB.Checked)
            {
                inspect = _floatBValid ? _floatB : null;
            }

            if (!inspect.HasValue)
            {
                lblFloatKind.Text = "Kind: —";
                lblFloatSign.Text = "Sign: —";
                lblFloatActualPrec.Text = "Precision: —";
                lblFloatExp.Text = "Exponent (e): —";
                lblFloatMant.Text = "Significand (m): —";
                lblFloatMantBits.Text = "m Bit Length: —";
                lblFloatTop.Text = "Top Bit: —";
                dgvFloatLimbs.Rows.Clear();
                ResetIeeeInspection();
                return;
            }

            ApFloat val = inspect.Value;

            lblFloatKind.Text = $"Kind: {val.KindName}";
            lblFloatSign.Text = $"Sign: {(val.IsNegative ? "Negative (-)" : "Positive (+)")}";
            lblFloatActualPrec.Text = $"Precision: {val.Precision} bits";

            if (val.IsFinite && !val.IsZero)
            {
                lblFloatExp.Text = $"Exponent (e): {val.RawExponent:N0}";
                string mantStr = val.Significand.ToString();
                if (mantStr.Length > 50)
                    mantStr = mantStr[..45] + "... (truncated)";
                lblFloatMant.Text = $"Significand (m): {mantStr}";
                lblFloatMantBits.Text = $"m Bit Length: {val.Significand.BitLength:N0} bits (Odd: {(!val.Significand.IsZero)})";
                lblFloatTop.Text = $"Top Bit: 2^{val.TopBit}";
            }
            else
            {
                lblFloatExp.Text = "Exponent (e): 0";
                lblFloatMant.Text = val.IsZero ? "Significand (m): 0" : "Significand (m): N/A (Special)";
                lblFloatMantBits.Text = "m Bit Length: 0";
                lblFloatTop.Text = "Top Bit: N/A";
            }

            // Populate Significand Limbs Table
            uint[] mantLimbs = val.RawMantissa;
            dgvFloatLimbs.Rows.Clear();
            if (mantLimbs.Length == 0)
            {
                dgvFloatLimbs.Rows.Add("[Empty / Zero]", "0", "0x00000000", "0000 0000 0000 0000 0000 0000 0000 0000");
            }
            else
            {
                for (int i = 0; i < mantLimbs.Length; i++)
                {
                    uint limb = mantLimbs[i];
                    string tag = i == 0 ? " [0] (LSB)" : i == mantLimbs.Length - 1 ? $" [{i}] (MSB)" : $" [{i}]";
                    dgvFloatLimbs.Rows.Add(
                        tag,
                        limb.ToString("N0", CultureInfo.InvariantCulture),
                        $"0x{limb:X8}",
                        FormatBinaryNibbles(limb)
                    );
                }
            }

            // Populate IEEE Bitfield Inspector
            UpdateIeeeBitfieldInspection(val);
        }

        private void ResetIeeeInspection()
        {
            lblIeeeSign.Text = "Sign bit: —";
            lblIeeeExp.Text = "Biased Exponent: —";
            lblIeeeFrac.Text = "Fraction field: —";
            lblIeeeClass.Text = "Category: —";
            lblIeeeBytes.Text = "Bytes (LE): —";
        }

        private void UpdateIeeeBitfieldInspection(ApFloat val)
        {
            IeeeFormat format = cmbIeeeInspectFormat.SelectedIndex switch
            {
                0 => IeeeFormat.Binary16,
                1 => IeeeFormat.Binary32,
                3 => IeeeFormat.Binary128,
                4 => IeeeFormat.Binary256,
                _ => IeeeFormat.Binary64,
            };

            try
            {
                byte[] bytesLe = val.ToIeeeBytes(format, RoundingMode.ToNearestEven, bigEndian: false);
                byte[] bytesBe = val.ToIeeeBytes(format, RoundingMode.ToNearestEven, bigEndian: true);

                // Build hex string
                var sbLe = new StringBuilder();
                foreach (byte b in bytesLe) sbLe.Append(b.ToString("X2")).Append(' ');
                lblIeeeBytes.Text = $"LE: [ {sbLe.ToString().Trim()} ]  |  BE: [ {BitConverter.ToString(bytesBe).Replace("-", " ")} ]";

                // Extract fields from little-endian bytes
                int width = format.Width;
                int fractionBits = format.Precision - 1;
                int expBits = format.ExponentBits;

                // Sign bit is bit (width - 1)
                int signByteIdx = (width - 1) / 8;
                int signBitInByte = (width - 1) % 8;
                bool signBit = (bytesLe[signByteIdx] & (1 << signBitInByte)) != 0;
                lblIeeeSign.Text = $"Sign bit: {(signBit ? "1 (-)" : "0 (+)")}";

                // Category
                if (val.IsNaN)
                {
                    lblIeeeClass.Text = "Category: Quiet NaN";
                    lblIeeeExp.Text = $"Biased Exponent: All 1s ({new string('1', expBits)}b)";
                    lblIeeeFrac.Text = "Fraction: Non-zero (Top bit set for QNaN)";
                }
                else if (val.IsInfinity)
                {
                    lblIeeeClass.Text = $"Category: {(val.IsNegative ? "-Infinity" : "+Infinity")}";
                    lblIeeeExp.Text = $"Biased Exponent: All 1s ({new string('1', expBits)}b)";
                    lblIeeeFrac.Text = "Fraction: 0";
                }
                else if (val.IsZero)
                {
                    lblIeeeClass.Text = $"Category: {(val.IsNegative ? "-Zero (-0)" : "+Zero (+0)")}";
                    lblIeeeExp.Text = "Biased Exponent: 0";
                    lblIeeeFrac.Text = "Fraction: 0";
                }
                else
                {
                    // Check if subnormal
                    long minExp = format.MinExponent;
                    bool isSubnormal = val.TopBit.HasValue && val.TopBit.Value < minExp;
                    lblIeeeClass.Text = $"Category: {(isSubnormal ? "Subnormal (Underflowed to subnormal range)" : "Normal finite value")}";

                    long biasedExp = val.RawExponent + format.Bias;
                    lblIeeeExp.Text = $"Stored Exp Field: {biasedExp} (Bias: {format.Bias}, Unbiased: {val.RawExponent})";
                    lblIeeeFrac.Text = $"Fraction: {fractionBits} bits reserved (implicit leading 1: {!isSubnormal})";
                }
            }
            catch (Exception ex)
            {
                lblIeeeClass.Text = $"Category: Error ({ex.Message})";
                lblIeeeBytes.Text = "Bytes: —";
            }
        }

        // =============================================================
        // Utility Helpers
        // =============================================================

        private static string FormatBinaryNibbles(uint value)
        {
            var sb = new StringBuilder(39);
            for (int i = 31; i >= 0; i--)
            {
                sb.Append(((value >> i) & 1) != 0 ? '1' : '0');
                if (i > 0 && (i % 4) == 0)
                {
                    sb.Append(' ');
                }
            }
            return sb.ToString();
        }

        private static void CopyToClipboard(string text)
        {
            if (!string.IsNullOrEmpty(text))
            {
                Clipboard.SetText(text);
            }
        }
    }
}
