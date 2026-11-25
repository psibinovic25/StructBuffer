#pragma once

struct Status
{
	int state;

	long pushForlen;
	int repeatFor;
	int idSeq;
	int repeatForDone;
	int SwingArm;
	long materialLen;
	int sawPos;
	int sawCylinder;
	int tool;


};

struct ServoStatus
{
	int st;
	long curPos;
	long setPoint;
	long positingErr;
	long maxSpd;
	long maxAcc;
	long maxDecc;
};

//comm
struct MachineRsp
{
        unsigned int cmdType;
	unsigned int id;
        unsigned long timeStamp;    
	Status mSt;
	ServoStatus mSrv;
        long ScannerPoints[10];

}; 

