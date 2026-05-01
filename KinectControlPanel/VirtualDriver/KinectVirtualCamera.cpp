#define WIN32_LEAN_AND_MEAN
#define NOMINMAX
#define INITGUID

#include <windows.h>
#include <objbase.h>
#include <initguid.h>
#include <unknwn.h>
#include <strmif.h>
#include <uuids.h>
#include <amvideo.h>
#include <vfwmsgs.h> // ✅ CORREÇÃO PRINCIPAL

#include <thread>
#include <atomic>
#include <cstring>

EXTERN_C IMAGE_DOS_HEADER __ImageBase;

#pragma comment(lib, "strmiids.lib")
#pragma comment(lib, "ole32.lib")
#pragma comment(lib, "oleaut32.lib")
#pragma comment(lib, "uuid.lib")

// {A1B2C3D4-E5F6-7890-ABCD-EF1234567890}
DEFINE_GUID(CLSID_KinectVirtualCamera,
0xa1b2c3d4,0xe5f6,0x7890,0xab,0xcd,0xef,0x12,0x34,0x56,0x78,0x90);

// ================= SHARED MEMORY =================
#define SHM_NAME L"KinectCam_SharedMem"
#define SHM_W 640
#define SHM_H 480
#define SHM_HDR 16

struct ShmHeader { UINT32 width,height,frameIndex,flags; };

static HANDLE g_hMap=nullptr;
static void* g_pView=nullptr;
static UINT32 g_lastFrame=0;

static void ShmOpen(){
    if(g_hMap)return;
    g_hMap=OpenFileMappingW(FILE_MAP_READ,FALSE,SHM_NAME);
    if(!g_hMap){
        UINT64 sz=SHM_HDR+(UINT64)SHM_W*SHM_H*4;
        g_hMap=CreateFileMappingW(INVALID_HANDLE_VALUE,nullptr,PAGE_READWRITE,
        (DWORD)(sz>>32),(DWORD)(sz&0xFFFFFFFF),SHM_NAME);
    }
    if(g_hMap)g_pView=MapViewOfFile(g_hMap,FILE_MAP_READ,0,0,0);
}

static void ShmClose(){
    if(g_pView){UnmapViewOfFile(g_pView);g_pView=nullptr;}
    if(g_hMap){CloseHandle(g_hMap);g_hMap=nullptr;}
}

static volatile LONG g_cObjects=0;
static volatile LONG g_cLocks=0;

static void FreeMediaType(AM_MEDIA_TYPE& mt){
    if(mt.cbFormat){CoTaskMemFree(mt.pbFormat);mt.cbFormat=0;mt.pbFormat=nullptr;}
}

static void FillMediaType(AM_MEDIA_TYPE* pmt){
    ZeroMemory(pmt,sizeof(*pmt));
    pmt->majortype=MEDIATYPE_Video;
    pmt->subtype=MEDIASUBTYPE_RGB32;
    pmt->formattype=FORMAT_VideoInfo;
    pmt->bFixedSizeSamples=TRUE;
    pmt->lSampleSize=SHM_W*SHM_H*4;

    auto* vih=(VIDEOINFOHEADER*)CoTaskMemAlloc(sizeof(VIDEOINFOHEADER));
    ZeroMemory(vih,sizeof(VIDEOINFOHEADER));

    vih->AvgTimePerFrame=333333;
    vih->bmiHeader.biSize=sizeof(BITMAPINFOHEADER);
    vih->bmiHeader.biWidth=SHM_W;
    vih->bmiHeader.biHeight=-SHM_H;
    vih->bmiHeader.biPlanes=1;
    vih->bmiHeader.biBitCount=32;
    vih->bmiHeader.biCompression=BI_RGB;
    vih->bmiHeader.biSizeImage=SHM_W*SHM_H*4;

    pmt->cbFormat=sizeof(VIDEOINFOHEADER);
    pmt->pbFormat=(BYTE*)vih;
}

// ================= PIN =================
class KinectFilter;

class KinectPin : public IPin {
    LONG ref;
    KinectFilter* filter;
public:
    KinectPin(KinectFilter* f):ref(1),filter(f){ShmOpen();}
    ~KinectPin(){ShmClose();}

    STDMETHODIMP QueryInterface(REFIID riid,void**ppv){
        if(riid==IID_IUnknown||riid==IID_IPin){*ppv=this;AddRef();return S_OK;}
        return E_NOINTERFACE;
    }
    STDMETHODIMP_(ULONG) AddRef(){return InterlockedIncrement(&ref);}
    STDMETHODIMP_(ULONG) Release(){ULONG r=InterlockedDecrement(&ref);if(!r)delete this;return r;}

    STDMETHODIMP Connect(IPin*,const AM_MEDIA_TYPE*){return E_NOTIMPL;}
    STDMETHODIMP ReceiveConnection(IPin*,const AM_MEDIA_TYPE*){return E_NOTIMPL;}
    STDMETHODIMP Disconnect(){return S_OK;}
    STDMETHODIMP ConnectedTo(IPin**){return VFW_E_NOT_CONNECTED;}
    STDMETHODIMP ConnectionMediaType(AM_MEDIA_TYPE*){return VFW_E_NOT_CONNECTED;}
    STDMETHODIMP QueryPinInfo(PIN_INFO* p){p->pFilter=nullptr;return S_OK;}
    STDMETHODIMP QueryDirection(PIN_DIRECTION* p){*p=PINDIR_OUTPUT;return S_OK;}
    STDMETHODIMP QueryId(LPWSTR* p){*p=(LPWSTR)CoTaskMemAlloc(10*sizeof(WCHAR));wcscpy_s(*p,10,L"Out");return S_OK;}
    STDMETHODIMP QueryAccept(const AM_MEDIA_TYPE*){return S_OK;}
    STDMETHODIMP EnumMediaTypes(IEnumMediaTypes**){return E_NOTIMPL;}
    STDMETHODIMP QueryInternalConnections(IPin**,ULONG*){return E_NOTIMPL;}
    STDMETHODIMP EndOfStream(){return S_OK;}
    STDMETHODIMP BeginFlush(){return S_OK;}
    STDMETHODIMP EndFlush(){return S_OK;}
    STDMETHODIMP NewSegment(REFERENCE_TIME,REFERENCE_TIME,double){return S_OK;}
};

// ================= FILTER =================
class KinectFilter : public IBaseFilter {
    LONG ref;
    KinectPin* pin;
public:
    KinectFilter():ref(1){
        InterlockedIncrement(&g_cObjects);
        pin=new KinectPin(this);
    }
    ~KinectFilter(){
        pin->Release();
        InterlockedDecrement(&g_cObjects);
    }

    STDMETHODIMP QueryInterface(REFIID riid,void**ppv){
        if(riid==IID_IUnknown||riid==IID_IBaseFilter){
            *ppv=this;AddRef();return S_OK;
        }
        return E_NOINTERFACE;
    }
    STDMETHODIMP_(ULONG) AddRef(){return InterlockedIncrement(&ref);}
    STDMETHODIMP_(ULONG) Release(){ULONG r=InterlockedDecrement(&ref);if(!r)delete this;return r;}

    STDMETHODIMP GetClassID(CLSID* p){*p=CLSID_KinectVirtualCamera;return S_OK;}
    STDMETHODIMP Stop(){return S_OK;}
    STDMETHODIMP Pause(){return S_OK;}
    STDMETHODIMP Run(REFERENCE_TIME){return S_OK;}
    STDMETHODIMP GetState(DWORD,FILTER_STATE* s){*s=State_Stopped;return S_OK;}
    STDMETHODIMP SetSyncSource(IReferenceClock*){return S_OK;}
    STDMETHODIMP GetSyncSource(IReferenceClock**){return S_OK;}
    STDMETHODIMP EnumPins(IEnumPins**){return E_NOTIMPL;}
    STDMETHODIMP FindPin(LPCWSTR,IPin**){return VFW_E_NOT_FOUND;}
    STDMETHODIMP QueryFilterInfo(FILTER_INFO*){return S_OK;}
    STDMETHODIMP JoinFilterGraph(IFilterGraph*,LPCWSTR){return S_OK;}
    STDMETHODIMP QueryVendorInfo(LPWSTR*){return E_NOTIMPL;}
};

// ================= FACTORY =================
class Factory : public IClassFactory{
    LONG ref;
public:
    Factory():ref(1){}
    STDMETHODIMP QueryInterface(REFIID riid,void**ppv){
        if(riid==IID_IUnknown||riid==IID_IClassFactory){
            *ppv=this;AddRef();return S_OK;
        }
        return E_NOINTERFACE;
    }
    STDMETHODIMP_(ULONG) AddRef(){return InterlockedIncrement(&ref);}
    STDMETHODIMP_(ULONG) Release(){ULONG r=InterlockedDecrement(&ref);if(!r)delete this;return r;}
    STDMETHODIMP CreateInstance(IUnknown*,REFIID riid,void**ppv){
        auto* obj=new KinectFilter();
        return obj->QueryInterface(riid,ppv);
    }
    STDMETHODIMP LockServer(BOOL){return S_OK;}
};

// ================= DLL =================
BOOL APIENTRY DllMain(HMODULE,DWORD,LPVOID){return TRUE;}

HRESULT WINAPI DllGetClassObject(REFCLSID r,REFIID riid,void**ppv){
    if(r!=CLSID_KinectVirtualCamera) return CLASS_E_CLASSNOTAVAILABLE;
    auto* f=new Factory();
    return f->QueryInterface(riid,ppv);
}

HRESULT WINAPI DllCanUnloadNow(){
    return (g_cObjects==0)?S_OK:S_FALSE;
}

HRESULT WINAPI DllRegisterServer(){
    WCHAR path[MAX_PATH];
    GetModuleFileNameW((HMODULE)&__ImageBase,path,MAX_PATH);

    HKEY h;
    RegCreateKeyExW(HKEY_CLASSES_ROOT,
    L"CLSID\\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}\\InprocServer32",
    0,nullptr,0,KEY_WRITE,nullptr,&h,nullptr);

    RegSetValueExW(h,nullptr,0,REG_SZ,(BYTE*)path,(DWORD)((wcslen(path)+1)*2));
    RegSetValueExW(h,L"ThreadingModel",0,REG_SZ,(BYTE*)L"Both",10);

    RegCloseKey(h);
    return S_OK;
}

HRESULT WINAPI DllUnregisterServer(){
    RegDeleteTreeW(HKEY_CLASSES_ROOT,
    L"CLSID\\{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}");
    return S_OK;
}