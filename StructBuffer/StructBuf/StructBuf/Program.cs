using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Linq;

class Program
{
    public class FieldInfo
    {
        public string Type { get; set; }
        public string Name { get; set; }
        public int ArraySize { get; set; } = 1;
        public bool IsCustomStruct { get; set; }
    }

    public class StructDefinition
    {
        public string Name { get; set; }
        public bool HasComm { get; set; }
        public List<FieldInfo> Fields { get; set; } = new List<FieldInfo>();
    }

    static void Main(string[] args)
    {
        // PUTANJA DO VAŠEG .h FAJLA NA DISKU
        string headerFilePath = @"D:\Razvoj\NaucniRadovi\StructBuffer\structures.h";

        // Izaberite arhitekturu: "nano" ili "due"
        string architecture = "nano";

        if (!File.Exists(headerFilePath))
        {
            Console.WriteLine($"Greška: Fajl ne postoji na putanji: {headerFilePath}");
            return;
        }

        string cppHeaderCode = File.ReadAllText(headerFilePath);
        var structs = ParseCppHeader(cppHeaderCode);

        // OBAVEZNO DODAVANJE Dbg STRUKTURE AKO JE NEMA U .h FAJLU
        EnsureDbgStructExists(structs);

        // 1. Generisanje Python modula za sirove binarne strukture
        string pythonModuleCode = GeneratePythonModule(structs, architecture);
        File.WriteAllText("arduino_packets.py", pythonModuleCode);
        Console.WriteLine($"Uspešno generisan fajl: arduino_packets.py (arhitektura: {architecture.ToUpper()})");

        // 2. Generisanje Python Binary Test skripte
        string pythonTestCode = GeneratePythonTestScript(structs, architecture);
        string pythonTestFileName = $"test_loopback_{architecture}.py";
        File.WriteAllText(pythonTestFileName, pythonTestCode);
        Console.WriteLine($"Uspešno generisan fajl: {pythonTestFileName}");

        // 3. Generisanje Arduino Binary Echo Loopback skeča
        string arduinoSketchCode = GenerateArduinoEchoSketch(structs, architecture);
        string arduinoFileName = $"ArduinoEchoLoopback_{architecture}.ino";
        File.WriteAllText(arduinoFileName, arduinoSketchCode);
        Console.WriteLine($"Uspešno generisan fajl: {arduinoFileName}");

        // 4. Generisanje Arduino MsgPack Loopback skeča
        string arduinoMsgPackCode = GenerateArduinoMsgPackSketch(structs, architecture);
        string arduinoMsgPackFileName = $"ArduinoEchoLoopback_msgpack_{architecture}.ino";
        File.WriteAllText(arduinoMsgPackFileName, arduinoMsgPackCode);
        Console.WriteLine($"Uspešno generisan fajl: {arduinoMsgPackFileName}");

        // 5. Generisanje Python MsgPack Test skripte
        string pythonMsgPackTestCode = GeneratePythonMsgPackTestScript(structs, architecture);
        string pythonMsgPackTestFileName = $"test_loopback_msgpack_{architecture}.py";
        File.WriteAllText(pythonMsgPackTestFileName, pythonMsgPackTestCode);
        Console.WriteLine($"Uspešno generisan fajl: {pythonMsgPackTestFileName}");
    }

    private static void EnsureDbgStructExists(List<StructDefinition> structs)
    {
        var dbgStruct = structs.FirstOrDefault(s => s.Name == "Dbg");
        if (dbgStruct == null)
        {
            dbgStruct = new StructDefinition
            {
                Name = "Dbg",
                HasComm = false,
                Fields = new List<FieldInfo>
                {
                    new FieldInfo { Type = "unsigned long", Name = "deser_time" },
                    new FieldInfo { Type = "unsigned long", Name = "ser_time" },
                    new FieldInfo { Type = "int", Name = "maxDeserRam" },
                    new FieldInfo { Type = "int", Name = "maxSerRam" }
                }
            };
            structs.Add(dbgStruct);
        }

        var commStruct = structs.FirstOrDefault(s => s.HasComm) ?? structs.FirstOrDefault();
        if (commStruct != null)
        {
            if (!commStruct.Fields.Any(f => f.Name == "dbg"))
            {
                commStruct.Fields.Add(new FieldInfo
                {
                    Type = "Dbg",
                    Name = "dbg",
                    ArraySize = 1,
                    IsCustomStruct = true
                });
            }
        }
    }

    public static List<StructDefinition> ParseCppHeader(string code)
    {
        var structs = new List<StructDefinition>();
        string pattern = @"(/{1,2}\s*comm|/\*comm\*/)?\s*struct\s+(\w+)\s*\{([^}]+)\}\s*;";
        var matches = Regex.Matches(code, pattern, RegexOptions.Singleline | RegexOptions.IgnoreCase);

        foreach (Match match in matches)
        {
            var st = new StructDefinition();
            st.HasComm = match.Groups[1].Success;
            st.Name = match.Groups[2].Value;
            string body = match.Groups[3].Value;

            var lines = body.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var line in lines)
            {
                string cleanLine = line.Trim();
                if (string.IsNullOrEmpty(cleanLine) || cleanLine.StartsWith("//")) continue;

                cleanLine = Regex.Replace(cleanLine, @"\s+", " ");
                var parts = cleanLine.Split(' ');

                if (parts.Length >= 2)
                {
                    string type = "";
                    string nameAndArray = "";

                    if ((parts[0] == "unsigned" || parts[0] == "signed" || parts[0] == "long") && parts.Length >= 3 &&
                        (parts[1] == "char" || parts[1] == "int" || parts[1] == "long" || parts[1] == "short"))
                    {
                        type = parts[0] + " " + parts[1];
                        nameAndArray = string.Join(" ", parts.Skip(2));
                    }
                    else
                    {
                        type = parts[0];
                        nameAndArray = string.Join(" ", parts.Skip(1));
                    }

                    var field = new FieldInfo { Type = type };
                    var arrayMatch = Regex.Match(nameAndArray, @"(\w+)\s*\[\s*(\d+)\s*\]");
                    if (arrayMatch.Success)
                    {
                        field.Name = arrayMatch.Groups[1].Value;
                        field.ArraySize = int.Parse(arrayMatch.Groups[2].Value);
                    }
                    else
                    {
                        field.Name = nameAndArray.Trim();
                        field.ArraySize = 1;
                    }

                    field.IsCustomStruct = !IsPrimitiveType(type);
                    st.Fields.Add(field);
                }
            }
            structs.Add(st);
        }

        if (structs.Any() && !structs.Any(s => s.HasComm))
        {
            var commCandidate = structs.LastOrDefault(s => s.Name.EndsWith("Rsp") || s.Name.EndsWith("Cmd")) ?? structs.Last();
            commCandidate.HasComm = true;
        }

        return structs;
    }

    private static bool IsPrimitiveType(string type)
    {
        string[] primitives = {
            "char", "signed char", "unsigned char", "int8_t", "uint8_t",
            "short", "unsigned short", "int16_t", "uint16_t",
            "int", "unsigned int", "long", "unsigned long", "int32_t", "uint32_t",
            "float", "double"
        };
        return primitives.Contains(type);
    }

    private static string GetMidValueForType(string type, string arch)
    {
        switch (type)
        {
            case "uint8_t":
            case "unsigned char":
                return "127";
            case "int8_t":
            case "char":
            case "signed char":
                return "0";
            case "uint16_t":
            case "unsigned short":
                return "32767";
            case "int16_t":
            case "short":
            case "signed short":
                return "0";
            case "unsigned int":
                return arch == "nano" ? "32767" : "2147483647";
            case "int":
            case "signed int":
                return "0";
            case "uint32_t":
            case "unsigned long":
                return "2147483647";
            case "int32_t":
            case "long":
            case "signed long":
                return "0";
            case "float":
            case "double":
                return "123.45";
            default:
                return "0";
        }
    }

    private static string GetStructFormatString(StructDefinition st, List<StructDefinition> allStructs, string arch)
    {
        string fmt = "";
        foreach (var field in st.Fields)
        {
            string charCode = GetStructCharCode(field.Type, arch);
            if (charCode != "")
            {
                fmt += new string(charCode[0], field.ArraySize);
            }
            else
            {
                var nestedSt = allStructs.FirstOrDefault(s => s.Name == field.Type);
                if (nestedSt != null)
                {
                    string nestedFmt = GetStructFormatString(nestedSt, allStructs, arch);
                    for (int i = 0; i < field.ArraySize; i++) fmt += nestedFmt;
                }
            }
        }
        return fmt;
    }

    private static string GetStructCharCode(string type, string arch)
    {
        switch (type)
        {
            case "int8_t":
            case "char":
            case "signed char": return "b";
            case "uint8_t":
            case "unsigned char": return "B";
            case "int16_t":
            case "short":
            case "signed short": return "h";
            case "uint16_t":
            case "unsigned short": return "H";
            case "int":
            case "signed int": return arch == "nano" ? "h" : "i";
            case "unsigned int": return arch == "nano" ? "H" : "I";
            case "int32_t":
            case "long":
            case "signed long": return "i";
            case "uint32_t":
            case "unsigned long": return "I";
            case "float": return "f";
            case "double": return arch == "nano" ? "f" : "d";
            default: return "";
        }
    }

    public static string GeneratePythonModule(List<StructDefinition> structs, string arch)
    {
        var py = new System.Text.StringBuilder();
        py.AppendLine($"# Automatski generisan Python fajl za arhitekturu: {arch.ToUpper()}");
        py.AppendLine("import struct");
        py.AppendLine();

        var structNamesToGenerate = new HashSet<string>(structs.Where(s => s.HasComm).Select(s => s.Name));
        foreach (var st in structs.Where(s => s.HasComm))
        {
            foreach (var field in st.Fields.Where(f => f.IsCustomStruct))
            {
                structNamesToGenerate.Add(field.Type);
            }
        }

        if (structNamesToGenerate.Count == 0)
        {
            foreach (var s in structs) structNamesToGenerate.Add(s.Name);
        }

        foreach (var st in structs)
        {
            if (structNamesToGenerate.Contains(st.Name))
            {
                py.AppendLine($"class {st.Name}:");
                py.AppendLine("    def __init__(self):");
                foreach (var field in st.Fields)
                {
                    string val = GetMidValueForType(field.Type, arch);
                    if (field.ArraySize > 1) py.AppendLine($"        self.{field.Name} = [{val}] * {field.ArraySize}");
                    else if (field.IsCustomStruct) py.AppendLine($"        self.{field.Name} = {field.Type}()");
                    else py.AppendLine($"        self.{field.Name} = {val}");
                }

                if (st.HasComm)
                {
                    string structFormat = GetStructFormatString(st, structs, arch);
                    py.AppendLine();
                    py.AppendLine("    def unpack(self, raw_bytes):");
                    py.AppendLine($"        unpacked_data = struct.unpack('<{structFormat}', raw_bytes)");
                    py.AppendLine("        idx = 0");
                    GenerateUnpackAssignments(st, structs, arch, py, "        ");
                    py.AppendLine("        return self");

                    py.AppendLine();
                    py.AppendLine("    def pack(self):");
                    py.AppendLine($"        return struct.pack('<{structFormat}',");
                    GeneratePackArguments(st, structs, py, "            ");
                    py.AppendLine("        )");
                }
                py.AppendLine();
            }
        }
        return py.ToString();
    }

    private static void GenerateUnpackAssignments(StructDefinition st, List<StructDefinition> allStructs, string arch, System.Text.StringBuilder py, string indent)
    {
        foreach (var field in st.Fields)
        {
            if (field.IsCustomStruct)
            {
                var nestedSt = allStructs.FirstOrDefault(s => s.Name == field.Type);
                if (nestedSt != null)
                {
                    foreach (var innerField in nestedSt.Fields)
                    {
                        if (innerField.ArraySize > 1 && !innerField.IsCustomStruct)
                        {
                            py.AppendLine($"{indent}self.{field.Name}.{innerField.Name} = list(unpacked_data[idx : idx + {innerField.ArraySize}])");
                            py.AppendLine($"{indent}idx += {innerField.ArraySize}");
                        }
                        else
                        {
                            py.AppendLine($"{indent}self.{field.Name}.{innerField.Name} = unpacked_data[idx]");
                            py.AppendLine($"{indent}idx += 1");
                        }
                    }
                }
            }
            else if (field.ArraySize > 1)
            {
                py.AppendLine($"{indent}self.{field.Name} = list(unpacked_data[idx : idx + {field.ArraySize}])");
                py.AppendLine($"{indent}idx += {field.ArraySize}");
            }
            else
            {
                py.AppendLine($"{indent}self.{field.Name} = unpacked_data[idx]");
                py.AppendLine($"{indent}idx += 1");
            }
        }
    }

    private static void GeneratePackArguments(StructDefinition st, List<StructDefinition> allStructs, System.Text.StringBuilder py, string indent)
    {
        foreach (var field in st.Fields)
        {
            if (field.IsCustomStruct)
            {
                var nestedSt = allStructs.FirstOrDefault(s => s.Name == field.Type);
                if (nestedSt != null)
                {
                    foreach (var innerField in nestedSt.Fields)
                    {
                        if (innerField.ArraySize > 1 && !innerField.IsCustomStruct)
                        {
                            for (int i = 0; i < innerField.ArraySize; i++)
                                py.AppendLine($"{indent}self.{field.Name}.{innerField.Name}[{i}],");
                        }
                        else
                        {
                            py.AppendLine($"{indent}self.{field.Name}.{innerField.Name},");
                        }
                    }
                }
            }
            else if (field.ArraySize > 1)
            {
                for (int i = 0; i < field.ArraySize; i++)
                    py.AppendLine($"{indent}self.{field.Name}[{i}],");
            }
            else
            {
                py.AppendLine($"{indent}self.{field.Name},");
            }
        }
    }

    public static string GeneratePythonTestScript(List<StructDefinition> structs, string arch)
    {
        var commStructs = structs.Where(s => s.HasComm).ToList();
        var mainStruct = commStructs.FirstOrDefault()?.Name ?? "MachineRsp";

        var py = new System.Text.StringBuilder();
        py.AppendLine($"# Python test skripta za arhitekturu: {arch.ToUpper()}");
        py.AppendLine("import serial");
        py.AppendLine("import time");
        py.AppendLine("from arduino_packets import " + string.Join(", ", commStructs.Select(s => s.Name)));
        py.AppendLine();
        py.AppendLine("ser = serial.Serial('COM3', 115200, timeout=1)");
        py.AppendLine("time.sleep(2)");
        py.AppendLine();
        py.AppendLine($"packet = {mainStruct}()");
        py.AppendLine("print('Pokretanje binarnog test loop-a (10 iteracija)...')");
        py.AppendLine();
        py.AppendLine("for i in range(10):");
        py.AppendLine("    packet.id = i");
        py.AppendLine("    raw_data = packet.pack()");
        py.AppendLine("    ser.write(raw_data)");
        py.AppendLine();
        py.AppendLine("    expected_size = len(raw_data)");
        py.AppendLine("    received_bytes = ser.read(expected_size)");
        py.AppendLine();
        py.AppendLine("    if len(received_bytes) == expected_size:");
        py.AppendLine("        response_packet = " + mainStruct + "()");
        py.AppendLine("        response_packet.unpack(received_bytes)");
        py.AppendLine("        print(f'[{i+1}] Uspešan Echo! ID: {response_packet.id}')");
        py.AppendLine("        print(f'      [MCU] Vreme deserijalizacije: {response_packet.dbg.deser_time} µs')");
        py.AppendLine("        print(f'      [MCU] Vreme serijalizacije:    {response_packet.dbg.ser_time} µs')");
        py.AppendLine("    else:");
        py.AppendLine("        print(f'[{i+1}] Greška: Nisu stigli svi bajtovi.')");
        py.AppendLine("    time.sleep(0.5)");
        py.AppendLine();
        py.AppendLine("ser.close()");
        return py.ToString();
    }

    public static string GenerateArduinoEchoSketch(List<StructDefinition> structs, string arch)
    {
        var commStructs = structs.Where(s => s.HasComm).ToList();
        var mainStruct = commStructs.FirstOrDefault()?.Name ?? "MachineRsp";

        var code = new System.Text.StringBuilder();
        code.AppendLine($"// Automatski generisan Arduino Binary Echo Loopback skeč za: {arch.ToUpper()}");
        code.AppendLine();
        code.AppendLine("#pragma pack(1)");

        foreach (var st in structs)
        {
            code.AppendLine($"struct {st.Name} {{");
            foreach (var f in st.Fields)
            {
                if (f.ArraySize > 1)
                    code.AppendLine($"  {f.Type} {f.Name}[{f.ArraySize}];");
                else
                    code.AppendLine($"  {f.Type} {f.Name};");
            }
            code.AppendLine("};");
            code.AppendLine();
        }
        code.AppendLine("#pragma pack()");
        code.AppendLine();
        code.AppendLine($"{mainStruct} incomingData;");
        code.AppendLine();

        if (arch == "nano")
        {
            code.AppendLine("int getFreeRam() {");
            code.AppendLine("  extern int __heap_start, *__brkval;");
            code.AppendLine("  int v;");
            code.AppendLine("  return (int) &v - (__brkval == 0 ? (int) &__heap_start : (int) __brkval);");
            code.AppendLine("}");
        }
        else
        {
            code.AppendLine("extern \"C\" char *sbrk(int incr);");
            code.AppendLine("int getFreeRam() {");
            code.AppendLine("  char top;");
            code.AppendLine("  return &top - reinterpret_cast<char*>(sbrk(0));");
            code.AppendLine("}");
        }

        code.AppendLine();
        code.AppendLine("void setup() {");
        code.AppendLine("  Serial.begin(115200);");
        code.AppendLine("  while(!Serial);");
        code.AppendLine("}");
        code.AppendLine();
        code.AppendLine("int maxDeserRamUsed;");
        code.AppendLine("int maxSerRamUsed;");
        code.AppendLine("unsigned long deserDuration;");
        code.AppendLine("unsigned long serDuration;");
        code.AppendLine();
        code.AppendLine("void loop() {");
        code.AppendLine($"  if (Serial.available() >= sizeof({mainStruct})) {{");
        code.AppendLine("    int initialRam = getFreeRam();");
        code.AppendLine("    int minRam = initialRam;");
        code.AppendLine();
        code.AppendLine("    unsigned long t1 = micros();");
        code.AppendLine($"    Serial.readBytes((char*)&incomingData, sizeof({mainStruct}));");
        code.AppendLine("    deserDuration = micros() - t1;");
        code.AppendLine();
        code.AppendLine("    int currentRam = getFreeRam();");
        code.AppendLine("    if (currentRam < minRam) minRam = currentRam;");
        code.AppendLine("    maxDeserRamUsed = getFreeRam();");
        code.AppendLine();
        code.AppendLine("    incomingData.id++;");
        code.AppendLine();
        code.AppendLine("    incomingData.dbg.deser_time = deserDuration;");
        code.AppendLine("    incomingData.dbg.ser_time = serDuration;");
        code.AppendLine("    incomingData.dbg.maxDeserRam = maxDeserRamUsed;");
        code.AppendLine("    incomingData.dbg.maxSerRam = maxSerRamUsed;");
        code.AppendLine();
        code.AppendLine("    unsigned long t2 = micros();");
        code.AppendLine($"    Serial.write((uint8_t*)&incomingData, sizeof({mainStruct}));");
        code.AppendLine("    serDuration = micros() - t2;");
        code.AppendLine();
        code.AppendLine("    currentRam = getFreeRam();");
        code.AppendLine("    if (currentRam < minRam) minRam = currentRam;");
        code.AppendLine("    maxSerRamUsed = getFreeRam();");
        code.AppendLine("  }");
        code.AppendLine("}");

        return code.ToString();
    }

    public static string GenerateArduinoMsgPackSketch1(List<StructDefinition> structs, string arch)
    {
        var commStructs = structs.Where(s => s.HasComm).ToList();
        var mainStruct = commStructs.FirstOrDefault();
        string mainStructName = mainStruct?.Name ?? "MachineRsp";

        var code = new System.Text.StringBuilder();
        code.AppendLine($"// Automatski generisan Arduino MsgPack Loopback skeč za: {arch.ToUpper()}");
        code.AppendLine("#include <ArduinoJson.h>");
        code.AppendLine("#pragma pack(1)");

        foreach (var st in structs)
        {
            code.AppendLine($"struct {st.Name} {{");
            foreach (var f in st.Fields)
            {
                if (f.ArraySize > 1)
                    code.AppendLine($"  {f.Type} {f.Name}[{f.ArraySize}];");
                else
                    code.AppendLine($"  {f.Type} {f.Name};");
            }
            code.AppendLine("};");
            code.AppendLine();
        }
        code.AppendLine("#pragma pack()");
        code.AppendLine();

        code.AppendLine($"{mainStructName} data;");
        code.AppendLine("uint8_t outBuffer[400];");
        code.AppendLine("StaticJsonDocument<400> doc;");
        code.AppendLine();
        code.AppendLine("unsigned long deserDuration;");
        code.AppendLine("unsigned long serDuration;");
        code.AppendLine("int maxSerRamUsed;");
        code.AppendLine();

        if (arch == "nano")
        {
            code.AppendLine("int getFreeRam() {");
            code.AppendLine("  extern int __heap_start, *__brkval;");
            code.AppendLine("  int v;");
            code.AppendLine("  return (int) &v - (__brkval == 0 ? (int) &__heap_start : (int) __brkval);");
            code.AppendLine("}");
        }
        else
        {
            code.AppendLine("extern \"C\" char *sbrk(int incr);");
            code.AppendLine("int getFreeRam() {");
            code.AppendLine("  char top;");
            code.AppendLine("  return &top - reinterpret_cast<char*>(sbrk(0));");
            code.AppendLine("}");
        }

        code.AppendLine();
        code.AppendLine("const size_t MAX_BUFFER_SIZE = 512;");
        code.AppendLine("uint8_t rxBuffer[MAX_BUFFER_SIZE];");
        code.AppendLine();
        code.AppendLine("size_t readSerialToBuffer(uint8_t* buffer, size_t maxCapacity) {");
        code.AppendLine("  if (Serial.available() == 0) return 0;");
        code.AppendLine("  delay(2);");
        code.AppendLine("  size_t bytesRead = 0;");
        code.AppendLine("  while (Serial.available() > 0 && bytesRead < maxCapacity) {");
        code.AppendLine("    buffer[bytesRead] = Serial.read();");
        code.AppendLine("    bytesRead++;");
        code.AppendLine("    delayMicroseconds(100);");
        code.AppendLine("  }");
        code.AppendLine("  if (bytesRead == maxCapacity && Serial.available() > 0) {");
        code.AppendLine("    while (Serial.available() > 0) Serial.read();");
        code.AppendLine("  }");
        code.AppendLine("  return bytesRead;");
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("void setup() {");
        code.AppendLine("  Serial.begin(115200);");
        code.AppendLine("  while (!Serial);");
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("void loop() {");
        code.AppendLine("  if (!Serial.available()) return;");
        code.AppendLine();
        code.AppendLine("  int initialRam = getFreeRam();");
        code.AppendLine("  int minRam = initialRam;");
        code.AppendLine();
        code.AppendLine("  size_t bytesRead = readSerialToBuffer(rxBuffer, MAX_BUFFER_SIZE);");
        code.AppendLine("  if (bytesRead == 0) return;");
        code.AppendLine();
        code.AppendLine("  unsigned long t1 = micros();");
        code.AppendLine("  DeserializationError error = deserializeMsgPack(doc, rxBuffer, bytesRead);");
        code.AppendLine("  deserDuration = micros() - t1;");
        code.AppendLine();
        code.AppendLine("  int currentRam = getFreeRam();");
        code.AppendLine("  if (currentRam < minRam) minRam = currentRam;");
        code.AppendLine("  int maxDeserRamUsed = getFreeRam();");
        code.AppendLine();
        code.AppendLine("  if (error) return;");
        code.AppendLine();

        if (mainStruct != null)
        {
            foreach (var f in mainStruct.Fields)
            {
                if (f.Name == "dbg") continue;

                if (f.IsCustomStruct)
                {
                    var nestedSt = structs.FirstOrDefault(s => s.Name == f.Type);
                    if (nestedSt != null)
                    {
                        foreach (var inner in nestedSt.Fields)
                        {
                            code.AppendLine($"  data.{f.Name}.{inner.Name} = doc[\"{f.Name}\"][\"{inner.Name}\"] | 0;");
                        }
                    }
                }
                else if (f.ArraySize > 1)
                {
                    code.AppendLine($"  JsonArray arr_{f.Name} = doc[\"{f.Name}\"];");
                    code.AppendLine($"  for (int i = 0; i < {f.ArraySize}; i++) {{");
                    code.AppendLine($"    data.{f.Name}[i] = arr_{f.Name}[i] | 0;");
                    code.AppendLine("  }");
                }
                else
                {
                    code.AppendLine($"  data.{f.Name} = doc[\"{f.Name}\"] | 0;");
                }
            }
        }

        code.AppendLine();
        code.AppendLine("  data.id++;");
        code.AppendLine("  doc.clear();");

        if (mainStruct != null)
        {
            foreach (var f in mainStruct.Fields)
            {
                if (f.Name == "dbg") continue;

                if (f.IsCustomStruct)
                {
                    var nestedSt = structs.FirstOrDefault(s => s.Name == f.Type);
                    if (nestedSt != null)
                    {
                        foreach (var inner in nestedSt.Fields)
                        {
                            code.AppendLine($"  doc[\"{f.Name}\"][\"{inner.Name}\"] = data.{f.Name}.{inner.Name};");
                        }
                    }
                }
                else if (f.ArraySize > 1)
                {
                    code.AppendLine($"  JsonArray arrOut_{f.Name} = doc.createNestedArray(\"{f.Name}\");");
                    code.AppendLine($"  for (int i = 0; i < {f.ArraySize}; i++) {{");
                    code.AppendLine($"    arrOut_{f.Name}.add(data.{f.Name}[i]);");
                    code.AppendLine("  }");
                }
                else
                {
                    code.AppendLine($"  doc[\"{f.Name}\"] = data.{f.Name};");
                }
            }
        }

        code.AppendLine();
        code.AppendLine("  doc[\"dbg\"][\"deser_time\"] = deserDuration;");
        code.AppendLine("  doc[\"dbg\"][\"maxDeserRam\"] = maxDeserRamUsed;");
        code.AppendLine("  doc[\"dbg\"][\"ser_time\"] = serDuration;");
        code.AppendLine("  doc[\"dbg\"][\"maxSerRam\"] = maxSerRamUsed;");
        code.AppendLine();
        code.AppendLine("  initialRam = getFreeRam();");
        code.AppendLine("  minRam = initialRam;");
        code.AppendLine();
        code.AppendLine("  unsigned long t2 = micros();");
        code.AppendLine("  size_t payloadLen = serializeMsgPack(doc, outBuffer, sizeof(outBuffer));");
        code.AppendLine("  serDuration = micros() - t2;");
        code.AppendLine();
        code.AppendLine("  if (payloadLen == 0) return;");
        code.AppendLine();
        code.AppendLine("  currentRam = getFreeRam();");
        code.AppendLine("  if (currentRam < minRam) minRam = currentRam;");
        code.AppendLine("  maxSerRamUsed = getFreeRam();");
        code.AppendLine();
        code.AppendLine("  Serial.write(outBuffer, payloadLen);");
        code.AppendLine("}");

        return code.ToString();
    }
    public static string GenerateArduinoMsgPackSketch(List<StructDefinition> structs, string arch)
    {
        var commStructs = structs.Where(s => s.HasComm).ToList();
        var mainStruct = commStructs.FirstOrDefault();
        string mainStructName = mainStruct?.Name ?? "MachineRsp";

        var code = new System.Text.StringBuilder();
        code.AppendLine($"// Automatski generisan Arduino MsgPack Loopback skeč za: {arch.ToUpper()}");
        code.AppendLine("#include <ArduinoJson.h>");
        code.AppendLine("#pragma pack(1)");

        foreach (var st in structs)
        {
            code.AppendLine($"struct {st.Name} {{");
            foreach (var f in st.Fields)
            {
                if (f.ArraySize > 1)
                    code.AppendLine($"  {f.Type} {f.Name}[{f.ArraySize}];");
                else
                    code.AppendLine($"  {f.Type} {f.Name};");
            }
            code.AppendLine("};");
            code.AppendLine();
        }
        code.AppendLine("#pragma pack()");
        code.AppendLine();

        code.AppendLine($"{mainStructName} data;");
        code.AppendLine("uint8_t outBuffer[400];");
        code.AppendLine("StaticJsonDocument<400> doc;");
        code.AppendLine();
        code.AppendLine("unsigned long deserDuration = 0;");
        code.AppendLine("unsigned long serDuration = 0;");
        code.AppendLine("int maxSerRamUsed = 0;");
        code.AppendLine();

        if (arch == "nano")
        {
            code.AppendLine("int getFreeRam() {");
            code.AppendLine("  extern int __heap_start, *__brkval;");
            code.AppendLine("  int v;");
            code.AppendLine("  return (int) &v - (__brkval == 0 ? (int) &__heap_start : (int) __brkval);");
            code.AppendLine("}");
        }
        else
        {
            code.AppendLine("extern \"C\" char *sbrk(int incr);");
            code.AppendLine("int getFreeRam() {");
            code.AppendLine("  char top;");
            code.AppendLine("  return &top - reinterpret_cast<char*>(sbrk(0));");
            code.AppendLine("}");
        }

        code.AppendLine();
        code.AppendLine("const size_t MAX_BUFFER_SIZE = 512;");
        code.AppendLine("uint8_t rxBuffer[MAX_BUFFER_SIZE];");
        code.AppendLine();
        code.AppendLine("size_t readSerialToBuffer(uint8_t* buffer, size_t maxCapacity) {");
        code.AppendLine("  if (Serial.available() == 0) return 0;");
        code.AppendLine("  delay(2);");
        code.AppendLine("  size_t bytesRead = 0;");
        code.AppendLine("  while (Serial.available() > 0 && bytesRead < maxCapacity) {");
        code.AppendLine("    buffer[bytesRead] = Serial.read();");
        code.AppendLine("    bytesRead++;");
        code.AppendLine("    delayMicroseconds(100);");
        code.AppendLine("  }");
        code.AppendLine("  if (bytesRead == maxCapacity && Serial.available() > 0) {");
        code.AppendLine("    while (Serial.available() > 0) Serial.read();");
        code.AppendLine("  }");
        code.AppendLine("  return bytesRead;");
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("void setup() {");
        code.AppendLine("  Serial.begin(115200);");
        code.AppendLine("  while (!Serial);");
        code.AppendLine("}");
        code.AppendLine();

        code.AppendLine("void loop() {");
        code.AppendLine("  if (!Serial.available()) return;");
        code.AppendLine();
        code.AppendLine("  int initialRam = getFreeRam();");
        code.AppendLine("  int minRam = initialRam;");
        code.AppendLine();
        code.AppendLine("  size_t bytesRead = readSerialToBuffer(rxBuffer, MAX_BUFFER_SIZE);");
        code.AppendLine("  if (bytesRead == 0) return;");
        code.AppendLine();
        code.AppendLine("  unsigned long t1 = micros();");
        code.AppendLine("  DeserializationError error = deserializeMsgPack(doc, rxBuffer, bytesRead);");
        code.AppendLine("  deserDuration = micros() - t1;");
        code.AppendLine();
        code.AppendLine("  int currentRam = getFreeRam();");
        code.AppendLine("  if (currentRam < minRam) minRam = currentRam;");
        code.AppendLine("  int maxDeserRamUsed = getFreeRam();");
        code.AppendLine();
        code.AppendLine("  if (error) return;");
        code.AppendLine();

        // Čitanje iz doc-a u strukturu (sa podrškom za nizove u ugnežđenim strukturama)
        if (mainStruct != null)
        {
            foreach (var f in mainStruct.Fields)
            {
                if (f.Name == "dbg") continue;

                if (f.IsCustomStruct)
                {
                    var nestedSt = structs.FirstOrDefault(s => s.Name == f.Type);
                    if (nestedSt != null)
                    {
                        foreach (var inner in nestedSt.Fields)
                        {
                            if (inner.ArraySize > 1)
                            {
                                code.AppendLine($"  JsonArray arr_{f.Name}_{inner.Name} = doc[\"{f.Name}\"][\"{inner.Name}\"];");
                                code.AppendLine($"  for (int i = 0; i < {inner.ArraySize}; i++) {{");
                                code.AppendLine($"    data.{f.Name}.{inner.Name}[i] = arr_{f.Name}_{inner.Name}[i] | 0;");
                                code.AppendLine("  }");
                            }
                            else
                            {
                                code.AppendLine($"  data.{f.Name}.{inner.Name} = doc[\"{f.Name}\"][\"{inner.Name}\"] | 0;");
                            }
                        }
                    }
                }
                else if (f.ArraySize > 1)
                {
                    code.AppendLine($"  JsonArray arr_{f.Name} = doc[\"{f.Name}\"];");
                    code.AppendLine($"  for (int i = 0; i < {f.ArraySize}; i++) {{");
                    code.AppendLine($"    data.{f.Name}[i] = arr_{f.Name}[i] | 0;");
                    code.AppendLine("  }");
                }
                else
                {
                    code.AppendLine($"  data.{f.Name} = doc[\"{f.Name}\"] | 0;");
                }
            }
        }

        code.AppendLine();
        code.AppendLine("  data.id++;");
        code.AppendLine("  doc.clear();");

        // Upis iz strukture nazad u doc (sa podrškom za nizove u ugnežđenim strukturama)
        if (mainStruct != null)
        {
            foreach (var f in mainStruct.Fields)
            {
                if (f.Name == "dbg") continue;

                if (f.IsCustomStruct)
                {
                    var nestedSt = structs.FirstOrDefault(s => s.Name == f.Type);
                    if (nestedSt != null)
                    {
                        foreach (var inner in nestedSt.Fields)
                        {
                            if (inner.ArraySize > 1)
                            {
                                code.AppendLine($"  JsonArray arrOut_{f.Name}_{inner.Name} = doc[\"{f.Name}\"][\"{inner.Name}\"].to<JsonArray>();");
                                code.AppendLine($"  for (int i = 0; i < {inner.ArraySize}; i++) {{");
                                code.AppendLine($"    arrOut_{f.Name}_{inner.Name}.add(data.{f.Name}.{inner.Name}[i]);");
                                code.AppendLine("  }");
                            }
                            else
                            {
                                code.AppendLine($"  doc[\"{f.Name}\"][\"{inner.Name}\"] = data.{f.Name}.{inner.Name};");
                            }
                        }
                    }
                }
                else if (f.ArraySize > 1)
                {
                    code.AppendLine($"  JsonArray arrOut_{f.Name} = doc[\"{f.Name}\"].to<JsonArray>();");
                    code.AppendLine($"  for (int i = 0; i < {f.ArraySize}; i++) {{");
                    code.AppendLine($"    arrOut_{f.Name}.add(data.{f.Name}[i]);");
                    code.AppendLine("  }");
                }
                else
                {
                    code.AppendLine($"  doc[\"{f.Name}\"] = data.{f.Name};");
                }
            }
        }

        code.AppendLine();
        code.AppendLine("  doc[\"dbg\"][\"deser_time\"] = deserDuration;");
        code.AppendLine("  doc[\"dbg\"][\"maxDeserRam\"] = maxDeserRamUsed;");
        code.AppendLine("  doc[\"dbg\"][\"ser_time\"] = serDuration;");
        code.AppendLine("  doc[\"dbg\"][\"maxSerRam\"] = maxSerRamUsed;");
        code.AppendLine();
        code.AppendLine("  initialRam = getFreeRam();");
        code.AppendLine("  minRam = initialRam;");
        code.AppendLine();
        code.AppendLine("  unsigned long t2 = micros();");
        code.AppendLine("  size_t payloadLen = serializeMsgPack(doc, outBuffer, sizeof(outBuffer));");
        code.AppendLine("  serDuration = micros() - t2;");
        code.AppendLine();
        code.AppendLine("  if (payloadLen == 0) return;");
        code.AppendLine();
        code.AppendLine("  currentRam = getFreeRam();");
        code.AppendLine("  if (currentRam < minRam) minRam = currentRam;");
        code.AppendLine("  maxSerRamUsed = getFreeRam();");
        code.AppendLine();
        code.AppendLine("  Serial.write(outBuffer, payloadLen);");
        code.AppendLine("}");

        return code.ToString();
    }
    public static string GeneratePythonMsgPackTestScript(List<StructDefinition> structs, string arch)
    {
        var commStructs = structs.Where(s => s.HasComm).ToList();
        var mainStruct = commStructs.FirstOrDefault();

        var py = new System.Text.StringBuilder();
        py.AppendLine($"# Python MsgPack Loopback Benchmark Test za arhitekturu: {arch.ToUpper()}");
        py.AppendLine("import serial");
        py.AppendLine("import time");
        py.AppendLine("import msgpack");
        py.AppendLine();
        py.AppendLine("ser = serial.Serial('COM3', 115200, timeout=2)");
        py.AppendLine("time.sleep(2)");
        py.AppendLine();
        py.AppendLine("data_payload = {");

        if (mainStruct != null)
        {
            foreach (var f in mainStruct.Fields)
            {
                if (f.IsCustomStruct && f.Name != "dbg")
                {
                    py.AppendLine($"    '{f.Name}': {{");
                    var nestedSt = structs.FirstOrDefault(s => s.Name == f.Type);
                    if (nestedSt != null)
                    {
                        foreach (var inner in nestedSt.Fields)
                        {
                            string val = GetMidValueForType(inner.Type, arch);
                            py.AppendLine($"        '{inner.Name}': {val},");
                        }
                    }
                    py.AppendLine("    },");
                }
                else if (f.Name == "dbg")
                {
                    continue;
                }
                else if (f.ArraySize > 1)
                {
                    string val = GetMidValueForType(f.Type, arch);
                    py.AppendLine($"    '{f.Name}': [{val}] * {f.ArraySize},");
                }
                else
                {
                    string val = GetMidValueForType(f.Type, arch);
                    py.AppendLine($"    '{f.Name}': {val},");
                }
            }
        }

        py.AppendLine("}");
        py.AppendLine("print('Pokretanje MsgPack test loop-a (10 iteracija)...')");
        py.AppendLine();
        py.AppendLine("for i in range(10):");
        py.AppendLine("    data_payload['id'] = i");
        py.AppendLine("    packed_data = msgpack.packb(data_payload)");
        py.AppendLine();
        py.AppendLine("    ser.reset_input_buffer()");
        py.AppendLine("    t_start = time.perf_counter()");
        py.AppendLine("    ser.write(packed_data)");
        py.AppendLine();
        py.AppendLine("    time.sleep(0.1)");
        py.AppendLine("    received_raw = ser.read(ser.in_waiting or 256)");
        py.AppendLine("    t_end = time.perf_counter()");
        py.AppendLine();
        py.AppendLine("    if received_raw:");
        py.AppendLine("        try:");
        py.AppendLine("            unpacked_response = msgpack.unpackb(received_raw)");
        py.AppendLine("            rtt_ms = (t_end - t_start) * 1000");
        py.AppendLine("            print(f'[{i+1}] MsgPack RTT: {rtt_ms:.3f} ms | Veličina: {len(received_raw)} B')");
        py.AppendLine("            if 'dbg' in unpacked_response:");
        py.AppendLine("                dbg = unpacked_response['dbg']");
        py.AppendLine("                print(f'      [MCU] Vreme deserijalizacije: {dbg.get(\"deser_time\", 0)} µs')");
        py.AppendLine("                print(f'      [MCU] Vreme serijalizacije:    {dbg.get(\"ser_time\", 0)} µs')");
        py.AppendLine("        except Exception as e:");
        py.AppendLine("            print(f'[{i+1}] Greška pri raspakivanju: {e} | Primljeno bajtova: {len(received_raw)}')");
        py.AppendLine("    else:");
        py.AppendLine("        print(f'[{i+1}] Greška: Nema odziva sa Arduina.')");
        py.AppendLine("    time.sleep(0.5)");
        py.AppendLine();
        py.AppendLine("ser.close()");

        return py.ToString();
    }
}