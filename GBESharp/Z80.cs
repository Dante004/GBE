using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GBESharp
{

    //Predkości procesora wykorzystywane przez GB
    public enum TimerFrequencyType
    {
        hz4096=0,
        hz262144=1,
        hz65536=2,
        hz16384=3
    }

    //Jest to zmodyfikowany Zilog Z80 
    class Z80
    {
        Memory memory;

        public Z80(Memory memory)
        {
            this.memory = memory;
        }

        int A, B, C, D, E, H, L; //Rejestry 8 bitowe
        int SP, PC; //Program counter Stack pointer
        bool FZ; // Flag zero
        bool FC; //Flag Carry
        bool FH; //Flag Half-Carry
        bool FN; //Flag Substraction
        public bool halted;
        public bool stopped;
        public static int ticks; //Cycle timers;
        public static bool timerRunning;
        public static int timerCounter;
        public static int timerModulo;
        public static TimerFrequencyType timerFrequency;
        public bool interruptsEnabled;
        public static bool vBlankInterruptRequested;
        public static bool vBlankInterruptEnabled;
        public static bool lcdControlOperationEnabled;
        public static bool lcdcInterruptEnabled, lcdcInterruptRequested;
        public static bool timerOverflowInterruptEnabled, timerOverflowInterruptRequested;
        public static bool serialIOTransferCompleteInterruptEnabled, serialIOTransferCompleteInterruptRequested;
        public static bool keyPressedInterruptEnabled, keyPressedInterruptRequested;
        public bool stopCounting;

        //Resetowanie flag, rejestrów i pamięci
        public void Reset()
        {
            A = 0x01;
            B = 0x00;
            C = 0x13;
            D = 0x00;
            E = 0xD8;
            H = 0x01;
            L = 0x4D;
            FZ = true;
            FC = false;
            FH = true;
            FN = true;
            SP = 0xFFFE;
            PC = 0x0100;

            memory.WriteByte(0xFF05, 0x00); // TIMA
            memory.WriteByte(0xFF06, 0x00); // TMA
            memory.WriteByte(0xFF07, 0x00); // TAC
            memory.WriteByte(0xFF10, 0x80); // NR10
            memory.WriteByte(0xFF11, 0xBF); // NR11
            memory.WriteByte(0xFF12, 0xF3); // NR12
            memory.WriteByte(0xFF14, 0xBF); // NR14
            memory.WriteByte(0xFF16, 0x3F); // NR21
            memory.WriteByte(0xFF17, 0x00); // NR22
            memory.WriteByte(0xFF19, 0xBF); // NR24
            memory.WriteByte(0xFF1A, 0x7F); // NR30
            memory.WriteByte(0xFF1B, 0xFF); // NR31
            memory.WriteByte(0xFF1C, 0x9F); // NR32
            memory.WriteByte(0xFF1E, 0xBF); // NR33
            memory.WriteByte(0xFF20, 0xFF); // NR41
            memory.WriteByte(0xFF21, 0x00); // NR42
            memory.WriteByte(0xFF22, 0x00); // NR43
            memory.WriteByte(0xFF23, 0xBF); // NR30
            memory.WriteByte(0xFF24, 0x77); // NR50
            memory.WriteByte(0xFF25, 0xF3); // NR51
            memory.WriteByte(0xFF26, 0xF1); // NR52
            memory.WriteByte(0xFF40, 0x91); // LCDC
            memory.WriteByte(0xFF42, 0x00); // SCY
            memory.WriteByte(0xFF43, 0x00); // SCX
            memory.WriteByte(0xFF45, 0x00); // LYC
            memory.WriteByte(0xFF47, 0xFC); // BGP
            memory.WriteByte(0xFF48, 0xFF); // OBP0
            memory.WriteByte(0xFF49, 0xFF); // OBP1
            memory.WriteByte(0xFF4A, 0x00); // WY
            memory.WriteByte(0xFF4B, 0x00); // WX
            memory.WriteByte(0xFFFF, 0x00); // IE
        }

        public void Step()
        {

            if (interruptsEnabled)
            {
                if (vBlankInterruptEnabled && vBlankInterruptRequested)
                {
                    vBlankInterruptRequested = false;
                    Interrupt(0x0040);
                }
                else if (lcdcInterruptEnabled && lcdcInterruptRequested)
                {
                    lcdcInterruptRequested = false;
                    Interrupt(0x0048);
                }
                else if (timerOverflowInterruptEnabled && timerOverflowInterruptRequested)
                {
                    timerOverflowInterruptRequested = false;
                    Interrupt(0x0050);
                }
                else if (serialIOTransferCompleteInterruptEnabled && serialIOTransferCompleteInterruptRequested)
                {
                    serialIOTransferCompleteInterruptRequested = false;
                    Interrupt(0x0058);
                }
                else if (keyPressedInterruptEnabled && keyPressedInterruptRequested)
                {
                    keyPressedInterruptRequested = false;
                    Interrupt(0x0060);
                }
            }

            PC &= 0xFFFF;

            int opCode = 0x00;
            if (!halted)
            {
                opCode = memory.ReadByte(PC); //Procesor pobier jakie instrukcje ma zrobić z własnej pamieci
                if (stopCounting)
                {
                    stopCounting = false;
                }
                else
                {
                    PC++;
                }
            }

            //Wybieranie instrukcji
            switch (opCode)
            {
                case 0x00: // NOP
                case 0xD3:
                case 0xDB:
                case 0xDD:
                case 0xE3:
                case 0xE4:
                case 0xEB:
                case 0xEC:
                case 0xF4:
                case 0xFC:
                case 0xFD:
                    NoOperation();
                    break;
                case 0x01: // LD BC,NN
                    LoadImmediate(ref B, ref C);
                    break;
                case 0x02: // LD (BC),A
                    WriteByte(B, C, A);
                    break;
                case 0x03: // INC BC
                    Increment(ref B, ref C);
                    break;
                case 0x04: // INC B
                    Increment(ref B);
                    break;
                case 0x05: // DEC B 
                    Decrement(ref B);
                    break;
                case 0x06: // LD B,N
                    LoadImmediate(ref B);
                    break;
                case 0x07: // RLCA
                    RotateALeft();
                    break;
                case 0x08: // LD (word),SP
                    WriteWordToImmediateAddress(SP);
                    break;
                case 0x09: // ADD HL,BC
                    Add(ref H, ref L, B, C);
                    break;
                case 0x0A: // LD A,(BC)
                    ReadByte(ref A, B, C);
                    break;
                case 0x0B: // DEC BC
                    Decrement(ref B, ref C);
                    break;
                case 0x0C: // INC C
                    Increment(ref C);
                    break;
                case 0x0D: // DEC C
                    Decrement(ref C);
                    break;
                case 0x0E: // LD C,N
                    LoadImmediate(ref C);
                    break;
                case 0x0F: // RRCA
                    RotateARight();
                    break;
                case 0x10: // STOP
                    stopped = true;
                    ticks += 4;
                    break;
                case 0x11: // LD DE,NN
                    LoadImmediate(ref D, ref E);
                    break;
                case 0x12: // LD (DE),A
                    WriteByte(D, E, A);
                    break;
                case 0x13: // INC DE
                    Increment(ref D, ref E);
                    break;
                case 0x14: // INC D
                    Increment(ref D);
                    break;
                case 0x15: // DEC D
                    Decrement(ref D);
                    break;
                case 0x16: // LD D,N
                    LoadImmediate(ref D);
                    break;
                case 0x17: // RLA
                    RotateALeftThroughCarry();
                    break;
                case 0x18: // JR N
                    JumpRelative();
                    break;
                case 0x19: // ADD HL,DE
                    Add(ref H, ref L, D, E);
                    break;
                case 0x1A: // LD A,(DE)
                    ReadByte(ref A, D, E);
                    break;
                case 0x1B: // DEC DE
                    Decrement(ref D, ref E);
                    break;
                case 0x1C: // INC E
                    Increment(ref E);
                    break;
                case 0x1D: // DEC E
                    Decrement(ref E);
                    break;
                case 0x1E: // LD E,N
                    LoadImmediate(ref E);
                    break;
                case 0x1F: // RRA
                    RotateARightThroughCarry();
                    break;
                case 0x20: // JR NZ,N
                    JumpRelativeIfNotZero();
                    break;
                case 0x21: // LD HL,NN
                    LoadImmediate(ref H, ref L);
                    break;
                case 0x22: // LD (HLI),A
                    WriteByte(H, L, A);
                    Increment(ref H, ref L);
                    break;
                case 0x23: // INC HL
                    Increment(ref H, ref L);
                    break;
                case 0x24: // INC H
                    Increment(ref H);
                    break;
                case 0x25: // DEC H
                    Decrement(ref H);
                    break;
                case 0x26: // LD H,N
                    LoadImmediate(ref H);
                    break;
                case 0x27: // DAA
                    DecimallyAdjustA();
                    break;
                case 0x28: // JR Z,N
                    JumpRelativeIfZero();
                    break;
                case 0x29: // ADD HL,HL
                    Add(ref H, ref L, H, L);
                    break;
                case 0x2A: // LD A,(HLI)
                    ReadByte(ref A, H, L);
                    Increment(ref H, ref L);
                    break;
                case 0x2B: // DEC HL
                    Decrement(ref H, ref L);
                    break;
                case 0x2C: // INC L
                    Increment(ref L);
                    break;
                case 0x2D: // DEC L
                    Decrement(ref L);
                    break;
                case 0x2E: // LD L,N
                    LoadImmediate(ref L);
                    break;
                case 0x2F: // CPL
                    ComplementA();
                    break;
                case 0x30: // JR NC,N
                    JumpRelativeIfNotCarry();
                    break;
                case 0x31: // LD SP,NN
                    LoadImmediateWord(ref SP);
                    break;
                case 0x32: // LD (HLD),A
                    WriteByte(H, L, A);
                    Decrement(ref H, ref L);
                    break;
                case 0x33: // INC SP
                    IncrementWord(ref SP);
                    break;
                case 0x34: // INC (HL)
                    IncrementMemory(H, L);
                    break;
                case 0x35: // DEC (HL)
                    DecrementMemory(H, L);
                    break;
                case 0x36: // LD (HL),N
                    LoadImmediateIntoMemory(H, L);
                    break;
                case 0x37: // SCF
                    SetCarryFlag();
                    break;
                case 0x38: // JR C,N
                    JumpRelativeIfCarry();
                    break;
                case 0x39: // ADD HL,SP
                    AddSPToHL();
                    break;
                case 0x3A: // LD A,(HLD)
                    ReadByte(ref A, H, L);
                    Decrement(ref H, ref L);
                    break;
                case 0x3B: // DEC SP
                    DecrementWord(ref SP);
                    break;
                case 0x3C: // INC A
                    Increment(ref A);
                    break;
                case 0x3D: // DEC A
                    Decrement(ref A);
                    break;
                case 0x3E: // LD A,N
                    LoadImmediate(ref A);
                    break;
                case 0x3F: // CCF
                    ComplementCarryFlag();
                    break;
                case 0x40: // LD B,B
                    Load(ref B, B);
                    break;
                case 0x41: // LD B,C
                    Load(ref B, C);
                    break;
                case 0x42: // LD B,D
                    Load(ref B, D);
                    break;
                case 0x43: // LD B,E
                    Load(ref B, E);
                    break;
                case 0x44: // LD B,H
                    Load(ref B, H);
                    break;
                case 0x45: // LD B,L
                    Load(ref B, L);
                    break;
                case 0x46: // LD B,(HL)
                    ReadByte(ref B, H, L);
                    break;
                case 0x47: // LD B,A
                    Load(ref B, A);
                    break;
                case 0x48: // LD C,B
                    Load(ref C, B);
                    break;
                case 0x49: // LD C,C
                    Load(ref C, C);
                    break;
                case 0x4A: // LD C,D
                    Load(ref C, D);
                    break;
                case 0x4B: // LD C,E
                    Load(ref C, E);
                    break;
                case 0x4C: // LD C,H
                    Load(ref C, H);
                    break;
                case 0x4D: // LD C,L
                    Load(ref C, L);
                    break;
                case 0x4E: // LD C,(HL)
                    ReadByte(ref C, H, L);
                    break;
                case 0x4F: // LD C,A
                    Load(ref C, A);
                    break;
                case 0x50: // LD D,B
                    Load(ref D, B);
                    break;
                case 0x51: // LD D,C
                    Load(ref D, C);
                    break;
                case 0x52: // LD D,D
                    Load(ref D, D);
                    break;
                case 0x53: // LD D,E
                    Load(ref D, E);
                    break;
                case 0x54: // LD D,H
                    Load(ref D, H);
                    break;
                case 0x55: // LD D,L
                    Load(ref D, L);
                    break;
                case 0x56: // LD D,(HL)
                    ReadByte(ref D, H, L);
                    break;
                case 0x57: // LD D,A
                    Load(ref D, A);
                    break;
                case 0x58: // LD E,B
                    Load(ref E, B);
                    break;
                case 0x59: // LD E,C
                    Load(ref E, C);
                    break;
                case 0x5A: // LD E,D
                    Load(ref E, D);
                    break;
                case 0x5B: // LD E,E
                    Load(ref E, E);
                    break;
                case 0x5C: // LD E,H
                    Load(ref E, H);
                    break;
                case 0x5D: // LD E,L
                    Load(ref E, L);
                    break;
                case 0x5E: // LD E,(HL)
                    ReadByte(ref E, H, L);
                    break;
                case 0x5F: // LD E,A
                    Load(ref E, A);
                    break;
                case 0x60: // LD H,B
                    Load(ref H, B);
                    break;
                case 0x61: // LD H,C
                    Load(ref H, C);
                    break;
                case 0x62: // LD H,D
                    Load(ref H, D);
                    break;
                case 0x63: // LD H,E
                    Load(ref H, E);
                    break;
                case 0x64: // LD H,H
                    Load(ref H, H);
                    break;
                case 0x65: // LD H,L
                    Load(ref H, L);
                    break;
                case 0x66: // LD H,(HL)
                    ReadByte(ref H, H, L);
                    break;
                case 0x67: // LD H,A
                    Load(ref H, A);
                    break;
                case 0x68: // LD L,B
                    Load(ref L, B);
                    break;
                case 0x69: // LD L,C
                    Load(ref L, C);
                    break;
                case 0x6A: // LD L,D
                    Load(ref L, D);
                    break;
                case 0x6B: // LD L,E
                    Load(ref L, E);
                    break;
                case 0x6C: // LD L,H
                    Load(ref L, H);
                    break;
                case 0x6D: // LD L,L
                    Load(ref L, L);
                    break;
                case 0x6E: // LD L,(HL)
                    ReadByte(ref L, H, L);
                    break;
                case 0x6F: // LD L,A
                    Load(ref L, A);
                    break;
                case 0x70: // LD (HL),B
                    WriteByte(H, L, B);
                    break;
                case 0x71: // LD (HL),C
                    WriteByte(H, L, C);
                    break;
                case 0x72: // LD (HL),D
                    WriteByte(H, L, D);
                    break;
                case 0x73: // LD (HL),E
                    WriteByte(H, L, E);
                    break;
                case 0x74: // LD (HL),H
                    WriteByte(H, L, H);
                    break;
                case 0x75: // LD (HL),L
                    WriteByte(H, L, L);
                    break;
                case 0x76: // HALT
                    Halt();
                    break;
                case 0x77: // LD (HL),A
                    WriteByte(H, L, A);
                    break;
                case 0x78: // LD A,B
                    Load(ref A, B);
                    break;
                case 0x79: // LD A,C
                    Load(ref A, C);
                    break;
                case 0x7A: // LD A,D
                    Load(ref A, D);
                    break;
                case 0x7B: // LD A,E
                    Load(ref A, E);
                    break;
                case 0x7C: // LD A,H
                    Load(ref A, H);
                    break;
                case 0x7D: // LD A,L
                    Load(ref A, L);
                    break;
                case 0x7E: // LD A,(HL)
                    ReadByte(ref A, H, L);
                    break;
                case 0x7F: // LD A,A
                    Load(ref A, A);
                    break;
                case 0x80: // ADD A,B
                    Add(B);
                    break;
                case 0x81: // ADD A,C
                    Add(C);
                    break;
                case 0x82: // ADD A,D
                    Add(D);
                    break;
                case 0x83: // ADD A,E
                    Add(E);
                    break;
                case 0x84: // ADD A,H
                    Add(H);
                    break;
                case 0x85: // ADD A,L
                    Add(L);
                    break;
                case 0x86: // ADD A,(HL)
                    Add(H, L);
                    break;
                case 0x87: // ADD A,A
                    Add(A);
                    break;
                case 0x88: // ADC A,B
                    AddWithCarry(B);
                    break;
                case 0x89: // ADC A,C
                    AddWithCarry(C);
                    break;
                case 0x8A: // ADC A,D
                    AddWithCarry(D);
                    break;
                case 0x8B: // ADC A,E
                    AddWithCarry(E);
                    break;
                case 0x8C: // ADC A,H
                    AddWithCarry(H);
                    break;
                case 0x8D: // ADC A,L
                    AddWithCarry(L);
                    break;
                case 0x8E: // ADC A,(HL)
                    AddWithCarry(H, L);
                    break;
                case 0x8F: // ADC A,A
                    AddWithCarry(A);
                    break;
                case 0x90: // SUB B
                    Sub(B);
                    break;
                case 0x91: // SUB C
                    Sub(C);
                    break;
                case 0x92: // SUB D
                    Sub(D);
                    break;
                case 0x93: // SUB E
                    Sub(E);
                    break;
                case 0x94: // SUB H
                    Sub(H);
                    break;
                case 0x95: // SUB L
                    Sub(L);
                    break;
                case 0x96: // SUB (HL)
                    Sub(H, L);
                    break;
                case 0x97: // SUB A
                    Sub(A);
                    break;
                case 0x98: // SBC B
                    SubWithBorrow(B);
                    break;
                case 0x99: // SBC C
                    SubWithBorrow(C);
                    break;
                case 0x9A: // SBC D
                    SubWithBorrow(D);
                    break;
                case 0x9B: // SBC E
                    SubWithBorrow(E);
                    break;
                case 0x9C: // SBC H
                    SubWithBorrow(H);
                    break;
                case 0x9D: // SBC L
                    SubWithBorrow(L);
                    break;
                case 0x9E: // SBC (HL)
                    SubWithBorrow(H, L);
                    break;
                case 0x9F: // SBC A
                    SubWithBorrow(A);
                    break;
                case 0xA0: // AND B
                    And(B);
                    break;
                case 0xA1: // AND C
                    And(C);
                    break;
                case 0xA2: // AND D
                    And(D);
                    break;
                case 0xA3: // AND E
                    And(E);
                    break;
                case 0xA4: // AND H
                    And(H);
                    break;
                case 0xA5: // AND L
                    And(L);
                    break;
                case 0xA6: // AND (HL)
                    And(H, L);
                    break;
                case 0xA7: // AND A
                    And(A);
                    break;
                case 0xA8: // XOR B
                    Xor(B);
                    break;
                case 0xA9: // XOR C
                    Xor(C);
                    break;
                case 0xAA: // XOR D
                    Xor(D);
                    break;
                case 0xAB: // XOR E
                    Xor(E);
                    break;
                case 0xAC: // XOR H
                    Xor(H);
                    break;
                case 0xAD: // XOR L
                    Xor(L);
                    break;
                case 0xAE: // XOR (HL)
                    Xor(H, L);
                    break;
                case 0xAF: // XOR A
                    Xor(A);
                    break;
                case 0xB0: // OR B
                    Or(B);
                    break;
                case 0xB1: // OR C
                    Or(C);
                    break;
                case 0xB2: // OR D
                    Or(D);
                    break;
                case 0xB3: // OR E
                    Or(E);
                    break;
                case 0xB4: // OR H
                    Or(H);
                    break;
                case 0xB5: // OR L
                    Or(L);
                    break;
                case 0xB6: // OR (HL)
                    Or(H, L);
                    break;
                case 0xB7: // OR A
                    Or(A);
                    break;
                case 0xB8: // CP B
                    Compare(B);
                    break;
                case 0xB9: // CP C
                    Compare(C);
                    break;
                case 0xBA: // CP D
                    Compare(D);
                    break;
                case 0xBB: // CP E
                    Compare(E);
                    break;
                case 0xBC: // CP H
                    Compare(H);
                    break;
                case 0xBD: // CP L
                    Compare(L);
                    break;
                case 0xBE: // CP (HL)
                    Compare(H, L);
                    break;
                case 0xBF: // CP A
                    Compare(A);
                    break;
                case 0xC0: // RET NZ
                    ReturnIfNotZero();
                    break;
                case 0xC1: // POP BC
                    Pop(ref B, ref C);
                    break;
                case 0xC2: // JP NZ,N
                    JumpIfNotZero();
                    break;
                case 0xC3: // JP N
                    Jump();
                    break;
                case 0xC4: // CALL NZ,NN
                    CallIfNotZero();
                    break;
                case 0xC5: // PUSH BC
                    Push(B, C);
                    break;
                case 0xC6: // ADD A,N
                    AddImmediate();
                    break;
                case 0xC7: // RST 00H
                    Restart(0);
                    break;
                case 0xC8: // RET Z
                    ReturnIfZero();
                    break;
                case 0xC9: // RET
                    Return();
                    break;
                case 0xCA: // JP Z,N
                    JumpIfZero();
                    break;
                case 0xCB:
                    switch (memory.ReadByte(PC++))
                    {
                        case 0x00: // RLC B
                            RotateLeft(ref B);
                            break;
                        case 0x01: // RLC C
                            RotateLeft(ref C);
                            break;
                        case 0x02: // RLC D
                            RotateLeft(ref D);
                            break;
                        case 0x03: // RLC E
                            RotateLeft(ref E);
                            break;
                        case 0x04: // RLC H
                            RotateLeft(ref H);
                            break;
                        case 0x05: // RLC L
                            RotateLeft(ref L);
                            break;
                        case 0x06: // RLC (HL)
                            RotateLeft(H, L);
                            break;
                        case 0x07: // RLC A
                            RotateLeft(ref A);
                            break;
                        case 0x08: // RRC B
                            RotateRight(ref B);
                            break;
                        case 0x09: // RRC C
                            RotateRight(ref C);
                            break;
                        case 0x0A: // RRC D
                            RotateRight(ref D);
                            break;
                        case 0x0B: // RRC E
                            RotateRight(ref E);
                            break;
                        case 0x0C: // RRC H
                            RotateRight(ref H);
                            break;
                        case 0x0D: // RRC L
                            RotateRight(ref L);
                            break;
                        case 0x0E: // RRC (HL)
                            RotateRight(H, L);
                            break;
                        case 0x0F: // RRC A
                            RotateRight(ref A);
                            break;
                        case 0x10: // RL  B
                            RotateLeftThroughCarry(ref B);
                            break;
                        case 0x11: // RL  C
                            RotateLeftThroughCarry(ref C);
                            break;
                        case 0x12: // RL  D
                            RotateLeftThroughCarry(ref D);
                            break;
                        case 0x13: // RL  E
                            RotateLeftThroughCarry(ref E);
                            break;
                        case 0x14: // RL  H
                            RotateLeftThroughCarry(ref H);
                            break;
                        case 0x15: // RL  L
                            RotateLeftThroughCarry(ref L);
                            break;
                        case 0x16: // RL  (HL)
                            RotateLeftThroughCarry(H, L);
                            break;
                        case 0x17: // RL  A
                            RotateLeftThroughCarry(ref A);
                            break;
                        case 0x18: // RR  B
                            RotateRightThroughCarry(ref B);
                            break;
                        case 0x19: // RR  C
                            RotateRightThroughCarry(ref C);
                            break;
                        case 0x1A: // RR  D
                            RotateRightThroughCarry(ref D);
                            break;
                        case 0x1B: // RR  E
                            RotateRightThroughCarry(ref E);
                            break;
                        case 0x1C: // RR  H
                            RotateRightThroughCarry(ref H);
                            break;
                        case 0x1D: // RR  L
                            RotateRightThroughCarry(ref L);
                            break;
                        case 0x1E: // RR  (HL)
                            RotateRightThroughCarry(H, L);
                            break;
                        case 0x1F: // RR  A
                            RotateRightThroughCarry(ref A);
                            break;
                        case 0x20: // SLA B
                            ShiftLeft(ref B);
                            break;
                        case 0x21: // SLA C
                            ShiftLeft(ref C);
                            break;
                        case 0x22: // SLA D
                            ShiftLeft(ref D);
                            break;
                        case 0x23: // SLA E
                            ShiftLeft(ref E);
                            break;
                        case 0x24: // SLA H
                            ShiftLeft(ref H);
                            break;
                        case 0x25: // SLA L
                            ShiftLeft(ref L);
                            break;
                        case 0x26: // SLA (HL)
                            ShiftLeft(H, L);
                            break;
                        case 0x27: // SLA A
                            ShiftLeft(ref A);
                            break;
                        case 0x28: // SRA B
                            SignedShiftRight(ref B);
                            break;
                        case 0x29: // SRA C
                            SignedShiftRight(ref C);
                            break;
                        case 0x2A: // SRA D
                            SignedShiftRight(ref D);
                            break;
                        case 0x2B: // SRA E
                            SignedShiftRight(ref E);
                            break;
                        case 0x2C: // SRA H
                            SignedShiftRight(ref H);
                            break;
                        case 0x2D: // SRA L
                            SignedShiftRight(ref L);
                            break;
                        case 0x2E: // SRA (HL)
                            SignedShiftRight(H, L);
                            break;
                        case 0x2F: // SRA A
                            SignedShiftRight(ref A);
                            break;
                        case 0x30: // SWAP B
                            Swap(ref B);
                            break;
                        case 0x31: // SWAP C
                            Swap(ref C);
                            break;
                        case 0x32: // SWAP D
                            Swap(ref D);
                            break;
                        case 0x33: // SWAP E
                            Swap(ref E);
                            break;
                        case 0x34: // SWAP H
                            Swap(ref H);
                            break;
                        case 0x35: // SWAP L
                            Swap(ref L);
                            break;
                        case 0x36: // SWAP (HL)
                            Swap(H, L);
                            break;
                        case 0x37: // SWAP A
                            Swap(ref A);
                            break;
                        case 0x38: // SRL B
                            UnsignedShiftRight(ref B);
                            break;
                        case 0x39: // SRL C
                            UnsignedShiftRight(ref C);
                            break;
                        case 0x3A: // SRL D
                            UnsignedShiftRight(ref D);
                            break;
                        case 0x3B: // SRL E
                            UnsignedShiftRight(ref E);
                            break;
                        case 0x3C: // SRL H
                            UnsignedShiftRight(ref H);
                            break;
                        case 0x3D: // SRL L
                            UnsignedShiftRight(ref L);
                            break;
                        case 0x3E: // SRL (HL)
                            UnsignedShiftRight(H, L);
                            break;
                        case 0x3F: // SRL A
                            UnsignedShiftRight(ref A);
                            break;
                        case 0x40: // BIT 0,B
                            TestBit(0, B);
                            break;
                        case 0x41: // BIT 0,C
                            TestBit(0, C);
                            break;
                        case 0x42: // BIT 0,D
                            TestBit(0, D);
                            break;
                        case 0x43: // BIT 0,E
                            TestBit(0, E);
                            break;
                        case 0x44: // BIT 0,H
                            TestBit(0, H);
                            break;
                        case 0x45: // BIT 0,L
                            TestBit(0, L);
                            break;
                        case 0x46: // BIT 0,(HL)
                            TestBit(0, H, L);
                            break;
                        case 0x47: // BIT 0,A
                            TestBit(0, A);
                            break;
                        case 0x48: // BIT 1,B
                            TestBit(1, B);
                            break;
                        case 0x49: // BIT 1,C
                            TestBit(1, C);
                            break;
                        case 0x4A: // BIT 1,D
                            TestBit(1, D);
                            break;
                        case 0x4B: // BIT 1,E
                            TestBit(1, E);
                            break;
                        case 0x4C: // BIT 1,H
                            TestBit(1, H);
                            break;
                        case 0x4D: // BIT 1,L
                            TestBit(1, L);
                            break;
                        case 0x4E: // BIT 1,(HL)
                            TestBit(1, H, L);
                            break;
                        case 0x4F: // BIT 1,A
                            TestBit(1, A);
                            break;
                        case 0x50: // BIT 2,B
                            TestBit(2, B);
                            break;
                        case 0x51: // BIT 2,C
                            TestBit(2, C);
                            break;
                        case 0x52: // BIT 2,D
                            TestBit(2, D);
                            break;
                        case 0x53: // BIT 2,E
                            TestBit(2, E);
                            break;
                        case 0x54: // BIT 2,H
                            TestBit(2, H);
                            break;
                        case 0x55: // BIT 2,L
                            TestBit(2, L);
                            break;
                        case 0x56: // BIT 2,(HL)
                            TestBit(2, H, L);
                            break;
                        case 0x57: // BIT 2,A
                            TestBit(2, A);
                            break;
                        case 0x58: // BIT 3,B
                            TestBit(3, B);
                            break;
                        case 0x59: // BIT 3,C
                            TestBit(3, C);
                            break;
                        case 0x5A: // BIT 3,D
                            TestBit(3, D);
                            break;
                        case 0x5B: // BIT 3,E
                            TestBit(3, E);
                            break;
                        case 0x5C: // BIT 3,H
                            TestBit(3, H);
                            break;
                        case 0x5D: // BIT 3,L
                            TestBit(3, L);
                            break;
                        case 0x5E: // BIT 3,(HL)
                            TestBit(3, H, L);
                            break;
                        case 0x5F: // BIT 3,A
                            TestBit(3, A);
                            break;
                        case 0x60: // BIT 4,B
                            TestBit(4, B);
                            break;
                        case 0x61: // BIT 4,C
                            TestBit(4, C);
                            break;
                        case 0x62: // BIT 4,D
                            TestBit(4, D);
                            break;
                        case 0x63: // BIT 4,E
                            TestBit(4, E);
                            break;
                        case 0x64: // BIT 4,H
                            TestBit(4, H);
                            break;
                        case 0x65: // BIT 4,L
                            TestBit(4, L);
                            break;
                        case 0x66: // BIT 4,(HL)
                            TestBit(4, H, L);
                            break;
                        case 0x67: // BIT 4,A
                            TestBit(4, A);
                            break;
                        case 0x68: // BIT 5,B
                            TestBit(5, B);
                            break;
                        case 0x69: // BIT 5,C
                            TestBit(5, C);
                            break;
                        case 0x6A: // BIT 5,D
                            TestBit(5, D);
                            break;
                        case 0x6B: // BIT 5,E
                            TestBit(5, E);
                            break;
                        case 0x6C: // BIT 5,H
                            TestBit(5, H);
                            break;
                        case 0x6D: // BIT 5,L
                            TestBit(5, L);
                            break;
                        case 0x6E: // BIT 5,(HL)
                            TestBit(5, H, L);
                            break;
                        case 0x6F: // BIT 5,A
                            TestBit(5, A);
                            break;
                        case 0x70: // BIT 6,B
                            TestBit(6, B);
                            break;
                        case 0x71: // BIT 6,C
                            TestBit(6, C);
                            break;
                        case 0x72: // BIT 6,D
                            TestBit(6, D);
                            break;
                        case 0x73: // BIT 6,E
                            TestBit(6, E);
                            break;
                        case 0x74: // BIT 6,H
                            TestBit(6, H);
                            break;
                        case 0x75: // BIT 6,L
                            TestBit(6, L);
                            break;
                        case 0x76: // BIT 6,(HL)
                            TestBit(6, H, L);
                            break;
                        case 0x77: // BIT 6,A
                            TestBit(6, A);
                            break;
                        case 0x78: // BIT 7,B
                            TestBit(7, B);
                            break;
                        case 0x79: // BIT 7,C
                            TestBit(7, C);
                            break;
                        case 0x7A: // BIT 7,D
                            TestBit(7, D);
                            break;
                        case 0x7B: // BIT 7,E
                            TestBit(7, E);
                            break;
                        case 0x7C: // BIT 7,H
                            TestBit(7, H);
                            break;
                        case 0x7D: // BIT 7,L
                            TestBit(7, L);
                            break;
                        case 0x7E: // BIT 7,(HL)
                            TestBit(7, H, L);
                            break;
                        case 0x7F: // BIT 7,A
                            TestBit(7, A);
                            break;
                        case 0x80: // RES 0,B
                            ResetBit(0, ref B);
                            break;
                        case 0x81: // RES 0,C
                            ResetBit(0, ref C);
                            break;
                        case 0x82: // RES 0,D
                            ResetBit(0, ref D);
                            break;
                        case 0x83: // RES 0,E
                            ResetBit(0, ref E);
                            break;
                        case 0x84: // RES 0,H
                            ResetBit(0, ref H);
                            break;
                        case 0x85: // RES 0,L
                            ResetBit(0, ref L);
                            break;
                        case 0x86: // RES 0,(HL)
                            ResetBit(0, H, L);
                            break;
                        case 0x87: // RES 0,A
                            ResetBit(0, ref A);
                            break;
                        case 0x88: // RES 1,B
                            ResetBit(1, ref B);
                            break;
                        case 0x89: // RES 1,C
                            ResetBit(1, ref C);
                            break;
                        case 0x8A: // RES 1,D
                            ResetBit(1, ref D);
                            break;
                        case 0x8B: // RES 1,E
                            ResetBit(1, ref E);
                            break;
                        case 0x8C: // RES 1,H
                            ResetBit(1, ref H);
                            break;
                        case 0x8D: // RES 1,L
                            ResetBit(1, ref L);
                            break;
                        case 0x8E: // RES 1,(HL)
                            ResetBit(1, H, L);
                            break;
                        case 0x8F: // RES 1,A
                            ResetBit(1, ref A);
                            break;
                        case 0x90: // RES 2,B
                            ResetBit(2, ref B);
                            break;
                        case 0x91: // RES 2,C
                            ResetBit(2, ref C);
                            break;
                        case 0x92: // RES 2,D
                            ResetBit(2, ref D);
                            break;
                        case 0x93: // RES 2,E
                            ResetBit(2, ref E);
                            break;
                        case 0x94: // RES 2,H
                            ResetBit(2, ref H);
                            break;
                        case 0x95: // RES 2,L
                            ResetBit(2, ref L);
                            break;
                        case 0x96: // RES 2,(HL)
                            ResetBit(2, H, L);
                            break;
                        case 0x97: // RES 2,A
                            ResetBit(2, ref A);
                            break;
                        case 0x98: // RES 3,B
                            ResetBit(3, ref B);
                            break;
                        case 0x99: // RES 3,C
                            ResetBit(3, ref C);
                            break;
                        case 0x9A: // RES 3,D
                            ResetBit(3, ref D);
                            break;
                        case 0x9B: // RES 3,E
                            ResetBit(3, ref E);
                            break;
                        case 0x9C: // RES 3,H
                            ResetBit(3, ref H);
                            break;
                        case 0x9D: // RES 3,L
                            ResetBit(3, ref L);
                            break;
                        case 0x9E: // RES 3,(HL)
                            ResetBit(3, H, L);
                            break;
                        case 0x9F: // RES 3,A
                            ResetBit(3, ref A);
                            break;
                        case 0xA0: // RES 4,B
                            ResetBit(4, ref B);
                            break;
                        case 0xA1: // RES 4,C
                            ResetBit(4, ref C);
                            break;
                        case 0xA2: // RES 4,D
                            ResetBit(4, ref D);
                            break;
                        case 0xA3: // RES 4,E
                            ResetBit(4, ref E);
                            break;
                        case 0xA4: // RES 4,H
                            ResetBit(4, ref H);
                            break;
                        case 0xA5: // RES 4,L
                            ResetBit(4, ref L);
                            break;
                        case 0xA6: // RES 4,(HL)
                            ResetBit(4, H, L);
                            break;
                        case 0xA7: // RES 4,A
                            ResetBit(4, ref A);
                            break;
                        case 0xA8: // RES 5,B
                            ResetBit(5, ref B);
                            break;
                        case 0xA9: // RES 5,C
                            ResetBit(5, ref C);
                            break;
                        case 0xAA: // RES 5,D
                            ResetBit(5, ref D);
                            break;
                        case 0xAB: // RES 5,E
                            ResetBit(5, ref E);
                            break;
                        case 0xAC: // RES 5,H
                            ResetBit(5, ref H);
                            break;
                        case 0xAD: // RES 5,L
                            ResetBit(5, ref L);
                            break;
                        case 0xAE: // RES 5,(HL)
                            ResetBit(5, H, L);
                            break;
                        case 0xAF: // RES 5,A
                            ResetBit(5, ref A);
                            break;
                        case 0xB0: // RES 6,B
                            ResetBit(6, ref B);
                            break;
                        case 0xB1: // RES 6,C
                            ResetBit(6, ref C);
                            break;
                        case 0xB2: // RES 6,D
                            ResetBit(6, ref D);
                            break;
                        case 0xB3: // RES 6,E
                            ResetBit(6, ref E);
                            break;
                        case 0xB4: // RES 6,H
                            ResetBit(6, ref H);
                            break;
                        case 0xB5: // RES 6,L
                            ResetBit(6, ref L);
                            break;
                        case 0xB6: // RES 6,(HL)
                            ResetBit(6, H, L);
                            break;
                        case 0xB7: // RES 6,A
                            ResetBit(6, ref A);
                            break;
                        case 0xB8: // RES 7,B
                            ResetBit(7, ref B);
                            break;
                        case 0xB9: // RES 7,C
                            ResetBit(7, ref C);
                            break;
                        case 0xBA: // RES 7,D
                            ResetBit(7, ref D);
                            break;
                        case 0xBB: // RES 7,E
                            ResetBit(7, ref E);
                            break;
                        case 0xBC: // RES 7,H
                            ResetBit(7, ref H);
                            break;
                        case 0xBD: // RES 7,L
                            ResetBit(7, ref L);
                            break;
                        case 0xBE: // RES 7,(HL)
                            ResetBit(7, H, L);
                            break;
                        case 0xBF: // RES 7,A
                            ResetBit(7, ref A);
                            break;
                        case 0xC0: // SET 0,B
                            SetBit(0, ref B);
                            break;
                        case 0xC1: // SET 0,C
                            SetBit(0, ref C);
                            break;
                        case 0xC2: // SET 0,D
                            SetBit(0, ref D);
                            break;
                        case 0xC3: // SET 0,E
                            SetBit(0, ref E);
                            break;
                        case 0xC4: // SET 0,H
                            SetBit(0, ref H);
                            break;
                        case 0xC5: // SET 0,L
                            SetBit(0, ref L);
                            break;
                        case 0xC6: // SET 0,(HL)
                            SetBit(0, H, L);
                            break;
                        case 0xC7: // SET 0,A
                            SetBit(0, ref A);
                            break;
                        case 0xC8: // SET 1,B
                            SetBit(1, ref B);
                            break;
                        case 0xC9: // SET 1,C
                            SetBit(1, ref C);
                            break;
                        case 0xCA: // SET 1,D
                            SetBit(1, ref D);
                            break;
                        case 0xCB: // SET 1,E
                            SetBit(1, ref E);
                            break;
                        case 0xCC: // SET 1,H
                            SetBit(1, ref H);
                            break;
                        case 0xCD: // SET 1,L
                            SetBit(1, ref L);
                            break;
                        case 0xCE: // SET 1,(HL)
                            SetBit(1, H, L);
                            break;
                        case 0xCF: // SET 1,A
                            SetBit(1, ref A);
                            break;
                        case 0xD0: // SET 2,B
                            SetBit(2, ref B);
                            break;
                        case 0xD1: // SET 2,C
                            SetBit(2, ref C);
                            break;
                        case 0xD2: // SET 2,D
                            SetBit(2, ref D);
                            break;
                        case 0xD3: // SET 2,E
                            SetBit(2, ref E);
                            break;
                        case 0xD4: // SET 2,H
                            SetBit(2, ref H);
                            break;
                        case 0xD5: // SET 2,L
                            SetBit(2, ref L);
                            break;
                        case 0xD6: // SET 2,(HL)
                            SetBit(2, H, L);
                            break;
                        case 0xD7: // SET 2,A
                            SetBit(2, ref A);
                            break;
                        case 0xD8: // SET 3,B
                            SetBit(3, ref B);
                            break;
                        case 0xD9: // SET 3,C
                            SetBit(3, ref C);
                            break;
                        case 0xDA: // SET 3,D
                            SetBit(3, ref D);
                            break;
                        case 0xDB: // SET 3,E
                            SetBit(3, ref E);
                            break;
                        case 0xDC: // SET 3,H
                            SetBit(3, ref H);
                            break;
                        case 0xDD: // SET 3,L
                            SetBit(3, ref L);
                            break;
                        case 0xDE: // SET 3,(HL)
                            SetBit(3, H, L);
                            break;
                        case 0xDF: // SET 3,A
                            SetBit(3, ref A);
                            break;
                        case 0xE0: // SET 4,B
                            SetBit(4, ref B);
                            break;
                        case 0xE1: // SET 4,C
                            SetBit(4, ref C);
                            break;
                        case 0xE2: // SET 4,D
                            SetBit(4, ref D);
                            break;
                        case 0xE3: // SET 4,E
                            SetBit(4, ref E);
                            break;
                        case 0xE4: // SET 4,H
                            SetBit(4, ref H);
                            break;
                        case 0xE5: // SET 4,L
                            SetBit(4, ref L);
                            break;
                        case 0xE6: // SET 4,(HL)
                            SetBit(4, H, L);
                            break;
                        case 0xE7: // SET 4,A
                            SetBit(4, ref A);
                            break;
                        case 0xE8: // SET 5,B
                            SetBit(5, ref B);
                            break;
                        case 0xE9: // SET 5,C
                            SetBit(5, ref C);
                            break;
                        case 0xEA: // SET 5,D
                            SetBit(5, ref D);
                            break;
                        case 0xEB: // SET 5,E
                            SetBit(5, ref E);
                            break;
                        case 0xEC: // SET 5,H
                            SetBit(5, ref H);
                            break;
                        case 0xED: // SET 5,L
                            SetBit(5, ref L);
                            break;
                        case 0xEE: // SET 5,(HL)
                            SetBit(5, H, L);
                            break;
                        case 0xEF: // SET 5,A
                            SetBit(5, ref A);
                            break;
                        case 0xF0: // SET 6,B
                            SetBit(6, ref B);
                            break;
                        case 0xF1: // SET 6,C
                            SetBit(6, ref C);
                            break;
                        case 0xF2: // SET 6,D
                            SetBit(6, ref D);
                            break;
                        case 0xF3: // SET 6,E
                            SetBit(6, ref E);
                            break;
                        case 0xF4: // SET 6,H
                            SetBit(6, ref H);
                            break;
                        case 0xF5: // SET 6,L
                            SetBit(6, ref L);
                            break;
                        case 0xF6: // SET 6,(HL)
                            SetBit(6, H, L);
                            break;
                        case 0xF7: // SET 6,A
                            SetBit(6, ref A);
                            break;
                        case 0xF8: // SET 7,B
                            SetBit(7, ref B);
                            break;
                        case 0xF9: // SET 7,C
                            SetBit(7, ref C);
                            break;
                        case 0xFA: // SET 7,D
                            SetBit(7, ref D);
                            break;
                        case 0xFB: // SET 7,E
                            SetBit(7, ref E);
                            break;
                        case 0xFC: // SET 7,H
                            SetBit(7, ref H);
                            break;
                        case 0xFD: // SET 7,L
                            SetBit(7, ref L);
                            break;
                        case 0xFE: // SET 7,(HL)
                            SetBit(7, H, L);
                            break;
                        case 0xFF: // SET 7,A
                            SetBit(7, ref A);
                            break;
                    }
                    break;
                case 0xCC: // CALL Z,NN
                    CallIfZero();
                    break;
                case 0xCD: // CALL NN
                    Call();
                    break;
                case 0xCE: // ADC A,N
                    AddImmediateWithCarry();
                    break;
                case 0xCF: // RST 8H
                    Restart(0x08);
                    break;
                case 0xD0: // RET NC
                    ReturnIfNotCarry();
                    break;
                case 0xD1: // POP DE
                    Pop(ref D, ref E);
                    break;
                case 0xD2: // JP NC,N
                    JumpIfNotCarry();
                    break;
                case 0xD4: // CALL NC,NN
                    CallIfNotCarry();
                    break;
                case 0xD5: // PUSH DE
                    Push(D, E);
                    break;
                case 0xD6: // SUB N
                    SubImmediate();
                    break;
                case 0xD7: // RST 10H
                    Restart(0x10);
                    break;
                case 0xD8: // RET C
                    ReturnIfCarry();
                    break;
                case 0xD9: // RETI
                    ReturnFromInterrupt();
                    break;
                case 0xDA: // JP C,N
                    JumpIfCarry();
                    break;
                case 0xDC: // CALL C,NN
                    CallIfCarry();
                    break;
                case 0xDE: // SBC A,N
                    SubImmediateWithBorrow();
                    break;
                case 0xDF: // RST 18H
                    Restart(0x18);
                    break;
                case 0xE0: // LD (FF00+byte),A
                    SaveAWithOffset();
                    break;
                case 0xE1: // POP HL
                    Pop(ref H, ref L);
                    break;
                case 0xE2: // LD (FF00+C),A
                    SaveAToC();
                    break;
                case 0xE5: // PUSH HL
                    Push(H, L);
                    break;
                case 0xE6: // AND N
                    AndImmediate();
                    break;
                case 0xE7: // RST 20H
                    Restart(0x20);
                    break;
                case 0xE8: // ADD SP,offset
                    OffsetStackPointer();
                    break;
                case 0xE9: // JP (HL)
                    Jump(H, L);
                    break;
                case 0xEA: // LD (word),A
                    SaveA();
                    break;
                case 0xEE: // XOR N
                    XorImmediate();
                    break;
                case 0xEF: // RST 28H
                    Restart(0x0028);
                    break;
                case 0xF0: // LD A, (FF00 + n)
                    LoadAFromImmediate();
                    break;
                case 0xF1: // POP AF
                    PopAF();
                    break;
                case 0xF2: // LD A, (FF00 + C)
                    LoadAFromC();
                    break;
                case 0xF3: // DI
                    interruptsEnabled = false;
                    break;
                case 0xF5: // PUSH AF
                    PushAF();
                    break;
                case 0xF6: // OR N
                    OrImmediate();
                    break;
                case 0xF7: // RST 30H
                    Restart(0x0030);
                    break;
                case 0xF8: // LD HL, SP + dd
                    LoadHLWithSPPlusImmediate();
                    break;
                case 0xF9: // LD SP,HL
                    LoadSPWithHL();
                    break;
                case 0xFA: // LD A, (nn)
                    LoadFromImmediateAddress(ref A);
                    break;
                case 0xFB: // EI
                    interruptsEnabled = true;
                    break;
                case 0xFE: // CP N
                    CompareImmediate();
                    break;
                case 0xFF: // RST 38H
                    Restart(0x0038);
                    break;
                default:
                    throw new Exception(string.Format("Unknown instruction: {0:X} at PC={1:X}", opCode, PC));
            }
        }

        //załadowanie rejestru b do a
        private void Load(ref int a, int b)
        {
            a = b;
            ticks += 4;
        }

        //załadowanie 16 bitowego rejetru HL do SP
        private void LoadSPWithHL()
        {
            SP = (H << 8) | L;
            ticks += 6;
        }

        //załaduj Rejestr A z pamięci
        private void LoadAFromImmediate()
        {
            A =  memory.ReadByte(0xFF00 | memory.ReadByte(PC++));
            ticks += 19;
        }

        //załadowanie A z C
        private void LoadAFromC()
        {
            A = memory.ReadByte(0xFF00 | C);
            ticks += 19;
        }

        private void LoadHLWithSPPlusImmediate()
        {
            int offset = memory.ReadByte(PC++);
            if (offset > 0x7F)
            {
                offset -= 256;
            }
            offset += SP;
            H = 0xFF & (offset >> 8);
            L = 0xFF & offset;
            ticks += 20;
        }

        private void ReturnFromInterrupt()
        {
            interruptsEnabled = true;
            halted = false;
            Return();
            ticks += 4;
        }

        //zwrócenie negatywnego a
        private void NegateA()
        {
            FC = A == 0;
            FH = (A & 0x0F) != 0;
            A = 0xFF & -A;
            FZ = A == 0;
            FN = true;
            ticks += 8;
        }

        //brak operacji
        private void NoOperation()
        {
            ticks += 4;
        }

        //załadowanie wartości z pamięci do sp
        private void OffsetStackPointer()
        {
            int value = memory.ReadByte(PC++);
            if (value > 0x7F)
            {
                value -= 256;
            }
            SP += value;
            ticks += 20;
        }

        //zapisanie wartości a do c
        private void SaveAToC()
        {
            memory.WriteByte(0xFF00 | C, A);
            ticks += 19;
        }

        //zapisanie rejestru a
        private void SaveA()
        {
            memory.WriteByte(memory.ReadWord(PC), A);
            PC += 2;
            ticks += 13;
        }

        private void SaveAWithOffset()
        {
            memory.WriteByte(0xFF00 | memory.ReadByte(PC++), A);
            ticks += 19;
        }

        //wymiana wartości w pamieci
        private void Swap(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            Swap(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void Swap(ref int r)
        {
            r = 0xFF & ((r << 4) | (r >> 4));
            ticks += 8;
        }

        //ustawienie bit w rejestrze
        private void SetBit(int bit, int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            SetBit(bit, ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void SetBit(int bit, ref int a)
        {
            a |= (1 << bit);
            ticks += 8;
        }

        //zresetowanie bitu w rejestrze
        private void ResetBit(int bit, int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            ResetBit(bit, ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void ResetBit(int bit, ref int a)
        {
            switch (bit)
            {
                case 0: // 1111 1110
                    a &= 0xFE;
                    break;
                case 1: // 1111 1101
                    a &= 0xFD;
                    break;
                case 2: // 1111 1011
                    a &= 0xFB;
                    break;
                case 3: // 1111 0111
                    a &= 0xF7;
                    break;
                case 4: // 1110 1111
                    a &= 0xEF;
                    break;
                case 5: // 1101 1111
                    a &= 0xDF;
                    break;
                case 6: // 1011 1111
                    a &= 0xBF;
                    break;
                case 7: // 0111 1111
                    a &= 0x7F;
                    break;
            }
            ticks += 8;
        }

        private void Halt()
        {
            if (interruptsEnabled)
            {
                halted = true;
            }
            else
            {
                stopCounting = true;
            }
            ticks += 4;
        }

        //testuje bit w rejestrze
        private void TestBit(int bit, int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            TestBit(bit, value);
            memory.WriteByte(address, value);
            ticks += 4;
        }

        private void TestBit(int bit, int a)
        {
            FZ = (a & (1 << bit)) == 0;
            FN = false;
            FH = true;
            ticks += 8;
        }

        private void CallIfCarry()
        {
            if (FC)
            {
                Call();
            }
            else
            {
                PC += 2;
                ticks++;
            }
        }

        private void CallIfNotCarry()
        {
            if (FC)
            {
                PC += 2;
                ticks++;
            }
            else
            {
                Call();
            }
        }

        private void CallIfZero()
        {
            if (FZ)
            {
                Call();
            }
            else
            {
                PC += 2;
                ticks++;
            }
        }

        private void CallIfNotZero()
        {
            if (FZ)
            {
                PC += 2;
                ticks++;
            }
            else
            {
                Call();
            }
        }

        private void Interrupt(int address)
        {
            interruptsEnabled = false;
            halted = false;
            Push(PC);
            PC = address;
        }

        private void Restart(int address)
        {
            Push(PC);
            PC = address;
        }

        private void Call()
        {
            Push(0xFFFF & (PC + 2));
            PC = memory.ReadWord(PC);
            ticks += 17;
        }

        private void JumpIfCarry()
        {
            if (FC)
            {
                Jump();
            }
            else
            {
                PC += 2;
                ticks++;
            }
        }

        private void JumpIfNotCarry()
        {
            if (FC)
            {
                PC += 2;
                ticks++;
            }
            else
            {
                Jump();
            }
        }

        private void JumpIfZero()
        {
            if (FZ)
            {
                Jump();
            }
            else
            {
                PC += 2;
                ticks++;
            }
        }

        private void JumpIfNotZero()
        {
            if (FZ)
            {
                PC += 2;
                ticks++;
            }
            else
            {
                Jump();
            }
        }

        private void Jump(int ah, int al)
        {
            PC = (ah << 8) | al;
            ticks += 4;
        }

        private void Jump()
        {
            PC = memory.ReadWord(PC);
            ticks += 10;
        }

        private void ReturnIfCarry()
        {
            if (FC)
            {
                Return();
                ticks++;
            }
            else
            {
                ticks += 5;
            }
        }

        private void ReturnIfNotCarry()
        {
            if (FC)
            {
                ticks += 5;
            }
            else
            {
                Return();
                ticks++;
            }
        }

        private void ReturnIfZero()
        {
            if (FZ)
            {
                Return();
                ticks++;
            }
            else
            {
                ticks += 5;
            }
        }

        private void ReturnIfNotZero()
        {
            if (FZ)
            {
                ticks += 5;
            }
            else
            {
                Return();
                ticks++;
            }
        }

        private void Return()
        {
            Pop(ref PC);
        }

        //załadowanie adresu do pamieci
        private void Pop(ref int a)
        {
            a = memory.ReadWord(SP);
            SP += 2;
            ticks += 10;
        }

        private void PopAF()
        {
            int F = 0;
            Pop(ref A, ref F);
            FZ = (F & 0x80) == 0x80;
            FC = (F & 0x40) == 0x40;
            FH = (F & 0x20) == 0x20;
            FN = (F & 0x10) == 0x10;
        }

        private void PushAF()
        {
            int F = 0;
            if (FZ)
            {
                F |= 0x80;
            }
            if (FC)
            {
                F |= 0x40;
            }
            if (FH)
            {
                F |= 0x20;
            }
            if (FN)
            {
                F |= 0x10;
            }
            Push(A, F);
        }

        private void Pop(ref int rh, ref int rl)
        {
            rl = memory.ReadByte(SP++);
            rh = memory.ReadByte(SP++);
            ticks += 10;
        }

        //zapisanie wartości do pamięci
        private void Push(int rh, int rl)
        {
            memory.WriteByte(--SP, rh);
            memory.WriteByte(--SP, rl);
            ticks += 11;
        }

        private void Push(int value)
        {
            SP -= 2;
            memory.WriteWord(SP, value);
            ticks += 11;
        }

        //działanie logiczne OR
        private void Or(int addressHigh, int addressLow)
        {
            Or(memory.ReadByte((addressHigh << 8) | addressLow));
            ticks += 3;
        }

        private void Or(int b)
        {
            A = 0xFF & (A | b);
            FH = false;
            FN = false;
            FC = false;
            FZ = A == 0;
            ticks += 4;
        }

        private void OrImmediate()
        {
            Or(memory.ReadByte(PC++));
            ticks += 3;
        }

        //działanie logiczne XOR
        private void XorImmediate()
        {
            Xor(memory.ReadByte(PC++));
        }

        private void Xor(int addressHigh, int addressLow)
        {
            Xor(memory.ReadByte((addressHigh << 8) | addressLow));
        }

        private void Xor(int b)
        {
            A = 0xFF & (A ^ b);
            FH = false;
            FN = false;
            FC = false;
            FZ = A == 0;
        }

        //działanie logiczne AND
        private void And(int addressHigh, int addressLow)
        {
            And(memory.ReadByte((addressHigh << 8) | addressLow));
            ticks += 3;
        }

        private void AndImmediate()
        {
            And(memory.ReadByte(PC++));
            ticks += 3;
        }

        private void And(int b)
        {
            A = 0xFF & (A & b);
            FH = true;
            FN = false;
            FC = false;
            FZ = A == 0;
            ticks += 4;
        }

        //ustawianie flagi carry
        private void SetCarryFlag()
        {
            FH = false;
            FC = true;
            FN = false;
            ticks += 4;
        }

        private void ComplementCarryFlag()
        {
            FH = FC;
            FC = !FC;
            FN = false;
            ticks += 4;
        }

        private void LoadImmediateIntoMemory(int ah, int al)
        {
            memory.WriteByte((ah << 8) | al, memory.ReadByte(PC++));
            ticks += 10;
        }

        private void ComplementA()
        {
            A ^= 0xFF;
            FN = true;
            FH = true;
            ticks += 4;
        }

        private void DecimallyAdjustA()
        {
            int highNibble = A >> 4;
            int lowNibble = A & 0x0F;
            bool _FC = true;
            if (FN)
            {
                if (FC)
                {
                    if (FH)
                    {
                        A += 0x9A;
                    }
                    else
                    {
                        A += 0xA0;
                    }
                }
                else
                {
                    _FC = false;
                    if (FH)
                    {
                        A += 0xFA;
                    }
                    else
                    {
                        A += 0x00;
                    }
                }
            }
            else if (FC)
            {
                if (FH || lowNibble > 9)
                {
                    A += 0x66;
                }
                else
                {
                    A += 0x60;
                }
            }
            else if (FH)
            {
                if (highNibble > 9)
                {
                    A += 0x66;
                }
                else
                {
                    A += 0x06;
                    _FC = false;
                }
            }
            else if (lowNibble > 9)
            {
                if (highNibble < 9)
                {
                    _FC = false;
                    A += 0x06;
                }
                else
                {
                    A += 0x66;
                }
            }
            else if (highNibble > 9)
            {
                A += 0x60;
            }
            else
            {
                _FC = false;
            }

            A &= 0xFF;
            FC = _FC;
            FZ = A == 0;
            ticks += 4;
        }

        private void JumpRelativeIfNotCarry()
        {
            if (FC)
            {
                PC++;
                ticks += 7;
            }
            else
            {
                JumpRelative();
            }
        }

        private void JumpRelativeIfCarry()
        {
            if (FC)
            {
                JumpRelative();
            }
            else
            {
                PC++;
                ticks += 7;
            }
        }

        private void JumpRelativeIfNotZero()
        {
            if (FZ)
            {
                PC++;
                ticks += 7;
            }
            else
            {
                JumpRelative();
            }
        }

        private void JumpRelativeIfZero()
        {
            if (FZ)
            {
                JumpRelative();
            }
            else
            {
                PC++;
                ticks += 7;
            }
        }

        private void JumpRelative()
        {
            int relativeAddress = memory.ReadByte(PC++);
            if (relativeAddress > 0x7F)
            {
                relativeAddress -= 256;
            }
            PC += relativeAddress;
            ticks += 12;
        }

        private void Add(int addressHigh, int addressLow)
        {
            Add(memory.ReadByte((addressHigh << 8) | addressLow));
            ticks += 3;
        }

        private void AddImmediateWithCarry()
        {
            AddWithCarry(memory.ReadByte(PC++));
            ticks += 3;
        }

        private void AddWithCarry(int addressHigh, int addressLow)
        {
            AddWithCarry(memory.ReadByte((addressHigh << 8) | addressLow));
            ticks += 3;
        }

        private void AddWithCarry(int b)
        {
            int carry = FC ? 1 : 0;
            FH = carry + (A & 0x0F) + (b & 0x0F) > 0x0F;
            A += b + carry;
            FC = A > 255;
            A &= 0xFF;
            FN = false;
            FZ = A == 0;
            ticks += 4;
        }

        private void SubWithBorrow(int ah, int al)
        {
            SubWithBorrow(memory.ReadByte((ah << 8) | al));
            ticks += 3;
        }

        private void SubImmediateWithBorrow()
        {
            SubWithBorrow(memory.ReadByte(PC++));
            ticks += 3;
        }

        private void SubWithBorrow(int b)
        {
            if (FC)
            {
                Sub(b + 1);
            }
            else
            {
                Sub(b);
            }
        }

        private void Sub(int ah, int al)
        {
            Sub(memory.ReadByte((ah << 8) | al));
            ticks += 3;
        }

        private void Compare(int ah, int al)
        {
            Compare(memory.ReadByte((ah << 8) | al));
            ticks += 3;
        }

        private void CompareImmediate()
        {
            Compare(memory.ReadByte(PC++));
            ticks += 3;
        }

        private void Compare(int b)
        {
            FH = (A & 0x0F) < (b & 0x0F);
            FC = b > A;
            FN = true;
            FZ = A == b;
            ticks += 4;
        }

        private void SubImmediate()
        {
            Sub(memory.ReadByte(PC++));
            ticks += 3;
        }

        private void Sub(int b)
        {
            FH = (A & 0x0F) < (b & 0x0F);
            FC = b > A;
            A -= b;
            A &= 0xFF;
            FN = true;
            FZ = A == 0;
            ticks += 4;
        }

        private void AddImmediate()
        {
            Add(memory.ReadByte(PC++));
            ticks += 3;
        }

        private void Add(int b)
        {
            FH = (A & 0x0F) + (b & 0x0F) > 0x0F;
            A += b;
            FC = A > 255;
            A &= 0xFF;
            FN = false;
            FZ = A == 0;
            ticks += 4;
        }

        private void AddSPToHL()
        {
            Add(ref H, ref L, SP >> 8, SP & 0xFF);
        }

        private void Add(ref int ah, ref int al, int bh, int bl)
        {
            al += bl;
            int carry = (al > 0xFF) ? 1 : 0;
            al &= 0xFF;
            FH = carry + (ah & 0x0F) + (bh & 0x0F) > 0x0F;
            ah += bh + carry;
            FC = ah > 0xFF;
            ah &= 0xFF;
            FN = false;
            ticks += 11;
        }

        private void ShiftLeft(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            ShiftLeft(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void ShiftLeft(ref int a)
        {
            FC = a > 0x7F;
            a = 0xFF & (a << 1);
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void UnsignedShiftRight(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            UnsignedShiftRight(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void UnsignedShiftRight(ref int a)
        {
            FC = (a & 0x01) == 1;
            a >>= 1;
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void SignedShiftRight(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            SignedShiftRight(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void SignedShiftRight(ref int a)
        {
            FC = (a & 0x01) == 1;
            a = (a & 0x80) | (a >> 1);
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void RotateARight()
        {
            int lowBit = A & 0x01;
            FC = lowBit == 1;
            A = (A >> 1) | (lowBit << 7);
            FN = false;
            FH = false;
            ticks += 4;
        }

        private void RotateARightThroughCarry()
        {
            int highBit = FC ? 0x80 : 0x00;
            FC = (A & 0x01) == 0x01;
            A = highBit | (A >> 1);
            FN = false;
            FH = false;
            ticks += 4;
        }

        private void RotateALeftThroughCarry()
        {
            int highBit = FC ? 1 : 0;
            FC = A > 0x7F;
            A = ((A << 1) & 0xFF) | highBit;
            FN = false;
            FH = false;
            ticks += 4;
        }

        private void RotateRight(ref int a)
        {
            int lowBit = a & 0x01;
            FC = lowBit == 1;
            a = (a >> 1) | (lowBit << 7);
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void RotateRightThroughCarry(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            RotateRightThroughCarry(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void RotateRightThroughCarry(ref int a)
        {
            int lowBit = FC ? 0x80 : 0;
            FC = (a & 0x01) == 1;
            a = (a >> 1) | lowBit;
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void RotateLeftThroughCarry(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            RotateLeftThroughCarry(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void RotateLeftThroughCarry(ref int a)
        {
            int highBit = FC ? 1 : 0;
            FC = (a >> 7) == 1;
            a = ((a << 1) & 0xFF) | highBit;
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void RotateLeft(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            RotateLeft(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void RotateRight(int ah, int al)
        {
            int address = (ah << 8) | al;
            int value = memory.ReadByte(address);
            RotateRight(ref value);
            memory.WriteByte(address, value);
            ticks += 7;
        }

        private void RotateLeft(ref int a)
        {
            int highBit = a >> 7;
            FC = highBit == 1;
            a = ((a << 1) & 0xFF) | highBit;
            FZ = a == 0;
            FN = false;
            FH = false;
            ticks += 8;
        }

        private void RotateALeft()
        {
            int highBit = A >> 7;
            FC = highBit == 1;
            A = ((A << 1) & 0xFF) | highBit;
            FN = false;
            FH = false;
            ticks += 4;
        }

        private void LoadFromImmediateAddress(ref int r)
        {
            r = memory.ReadByte(memory.ReadWord(PC));
            PC += 2;
            ticks += 13;
        }

        private void LoadImmediate(ref int r)
        {
            r = memory.ReadByte(PC++);
            ticks += 7;
        }

        private void LoadImmediateWord(ref int r)
        {
            r = memory.ReadWord(PC);
            PC += 2;
            ticks += 10;
        }

        private void LoadImmediate(ref int rh, ref int rl)
        {
            rl = memory.ReadByte(PC++);
            rh = memory.ReadByte(PC++);
            ticks += 10;
        }

        private void ReadByte(ref int r, int ah, int al)
        {
            r = memory.ReadByte((ah << 8) | al);
            ticks += 7;
        }

        private void WriteByte(int ah, int al, int value)
        {
            memory.WriteByte((ah << 8) | al, value);
            ticks += 7;
        }

        private void WriteWordToImmediateAddress(int value)
        {
            memory.WriteWord(memory.ReadWord(PC), value);
            PC += 2;
            ticks += 20;
        }

        private void Decrement(ref int rh, ref int rl)
        {
            if (rl == 0)
            {
                rh = 0xFF & (rh - 1);
                rl = 0xFF;
            }
            else
            {
                rl--;
            }
            ticks += 6;
        }

        private void IncrementWord(ref int r)
        {
            if (r == 0xFFFF)
            {
                r = 0;
            }
            else
            {
                r++;
            }
            ticks += 6;
        }

        private void DecrementWord(ref int r)
        {
            if (r == 0)
            {
                r = 0xFFFF;
            }
            else
            {
                r--;
            }
            ticks += 6;
        }

        private void DecrementMemory(int ah, int al)
        {
            int address = (ah << 8) | al;
            int r = memory.ReadByte(address);
            Decrement(ref r);
            memory.WriteByte(address, r);
            ticks += 7;
        }

        private void IncrementMemory(int ah, int al)
        {
            int address = (ah << 8) | al;
            int r = memory.ReadByte(address);
            Increment(ref r);
            memory.WriteByte(address, r);
            ticks += 7;
        }

        private void Increment(ref int rh, ref int rl)
        {
            if (rl == 255)
            {
                rh = 0xFF & (rh + 1);
                rl = 0;
            }
            else
            {
                rl++;
            }
            ticks += 6;
        }

        private void Increment(ref int r)
        {
            FH = (r & 0x0F) == 0x0F;
            r++;
            r &= 0xFF;
            FZ = r == 0;
            FN = false;
            ticks += 4;
        }

        private void Decrement(ref int r)
        {
            FH = (r & 0x0F) == 0x00;
            r--;
            r &= 0xFF;
            FZ = r == 0;
            FN = true;
            ticks += 4;
        }

    }
}
