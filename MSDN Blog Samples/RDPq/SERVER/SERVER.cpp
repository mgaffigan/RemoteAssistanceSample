/******
	Remote Desktop Sharing
	Athenian & wap2k - Rohitab forums
	http://www.rohitab.com
******/

/*THE SERVER (SHARER)*/
#define _CRT_SECURE_NO_WARNINGS

#include ...
#include ...
#include ...
#include ...

//this is to prevent noobs and total idiots like you from using this masterpiece!

using namespace Gdiplus;

#define APP MAKEINTRESOURCE(101)
#define APPSMALL MAKEINTRESOURCE(102)
#define MAX_ATTENDEE 1
#define override

HWND hwnd, startSharing, stopSharing, infoLog;
LRESULT CALLBACK WndProc(HWND, UINT, WPARAM, LPARAM);
static char className[] = "INVITER";
static HINSTANCE instance = NULL;

IRDPSRAPISharingSession *session = NULL;
IRDPSRAPIInvitationManager *invitationManager = NULL;
IRDPSRAPIInvitation *invitation = NULL;
IRDPSRAPIAttendeeManager *attendeeManager = NULL;
IRDPSRAPIAttendee *attendee = NULL;

IConnectionPointContainer* picpc = NULL;
IConnectionPoint* picp = NULL;

/*Event function prototypes*/
void OnAttendeeConnected(IDispatch *pAttendee);
void OnAttendeeDisconnected(IDispatch *pAttendee);
void OnControlLevelChangeRequest(IDispatch  *pAttendee, CTRL_LEVEL RequestedLevel);

class EventSink : public _IRDPSessionEvents {
public:
	EventSink(){
	}

	~EventSink(){
	}

	// IUnknown
	virtual HRESULT STDMETHODCALLTYPE override QueryInterface(
		REFIID iid, void**ppvObject){
		*ppvObject = 0;
		if (iid == IID_IUnknown || iid == IID_IDispatch || iid == __uuidof(_IRDPSessionEvents))
			*ppvObject = this;
		if (*ppvObject)
		{
			((IUnknown*)(*ppvObject))->AddRef();
			return S_OK;
		}
		return E_NOINTERFACE;
	}

	virtual ULONG STDMETHODCALLTYPE override AddRef(void){
		return 0;
	}

	virtual ULONG STDMETHODCALLTYPE override Release(void){
		return 0;
	}


	// IDispatch
	virtual HRESULT STDMETHODCALLTYPE override GetTypeInfoCount(
		__RPC__out UINT *pctinfo){
		return E_NOTIMPL;
	}

	virtual HRESULT STDMETHODCALLTYPE override GetTypeInfo(
		UINT iTInfo,
		LCID lcid,
		__RPC__deref_out_opt ITypeInfo **ppTInfo){
		return E_NOTIMPL;
	}

	virtual HRESULT STDMETHODCALLTYPE override GetIDsOfNames(
		__RPC__in REFIID riid,
		__RPC__in_ecount_full(cNames) LPOLESTR *rgszNames,
		UINT cNames,
		LCID lcid,
		__RPC__out_ecount_full(cNames) DISPID *rgDispId){
		return E_NOTIMPL;
	}

	virtual HRESULT STDMETHODCALLTYPE override EventSink::Invoke(
		DISPID dispIdMember,
		REFIID riid,
		LCID lcid,
		WORD wFlags,
		DISPPARAMS FAR* pDispParams,
		VARIANT FAR* pVarResult,
		EXCEPINFO FAR* pExcepInfo,
		unsigned int FAR* puArgErr){


		switch (dispIdMember){
		case DISPID_RDPSRAPI_EVENT_ON_ATTENDEE_CONNECTED:
			OnAttendeeConnected(pDispParams->rgvarg[0].pdispVal);
			break;
		case DISPID_RDPSRAPI_EVENT_ON_ATTENDEE_DISCONNECTED:
			OnAttendeeDisconnected(pDispParams->rgvarg[0].pdispVal);
			break;
		case DISPID_RDPSRAPI_EVENT_ON_CTRLLEVEL_CHANGE_REQUEST:
			OnControlLevelChangeRequest(pDispParams->rgvarg[1].pdispVal, (CTRL_LEVEL)pDispParams->rgvarg[0].intVal);
			break;
		}
		return S_OK;
	}
};

EventSink ev;

void AddText(HWND edit, LPCTSTR Text)
{
	int len = GetWindowTextLength(edit);
	SendMessage(edit, EM_SETSEL, (WPARAM)len, (LPARAM)len);
	SendMessage(edit, EM_REPLACESEL, 0, (LPARAM)Text);
}

char *saveFile(){
	char *ret = new char[1000];
	memset(ret, 0, 1000);
	OPENFILENAME name = { 0 };
	ZeroMemory(&name, sizeof(name));
	name.lStructSize = sizeof(name);
	name.hwndOwner = hwnd;
	name.lpstrFile = ret;
	name.lpstrFile[0] = 0;
	name.nMaxFile = sizeof(name);
	name.nFilterIndex = 1;
	name.lpstrDefExt = "xml";
	name.lpstrFilter = "XML\0*.xml\0";
	name.lpstrFileTitle = 0;
	name.nMaxFileTitle = 0;
	name.lpstrInitialDir = 0;
	name.Flags = OFN_PATHMUSTEXIST | OFN_FILEMUSTEXIST;
	if (GetSaveFileName(&name)) return ret;
	return NULL;
}

char *dupcat(const char *s1, ...){
	int len;
	char *p, *q, *sn;
	va_list ap;

	len = strlen(s1);
	va_start(ap, s1);
	while (1) {
		sn = va_arg(ap, char *);
		if (!sn)
			break;
		len += strlen(sn);
	}
	va_end(ap);

	p = new char[len + 1];
	strcpy(p, s1);
	q = p + strlen(p);

	va_start(ap, s1);
	while (1) {
		sn = va_arg(ap, char *);
		if (!sn)
			break;
		strcpy(q, sn);
		q += strlen(q);
	}
	va_end(ap);

	return p;
}

void GDIPLUS(HDC hdc, int drvIndex = -1){
	Graphics graphics(hdc);
	wchar_t *text = L"Remote Desktop Sharing";
	wchar_t *author = L"Athenian & wap2k - Rohitab forums";

	FontFamily family(L"Verdana");
	Font font(&family, 15, FontStyleRegular, UnitPixel);

	SolidBrush bBrush(Color(255, 0, 100, 200));
	// Fill the rectangle.
	graphics.FillRectangle(&bBrush, 0, 0, 800, 85);

	SolidBrush sbrush(Color::Black);
	graphics.DrawString(text, wcslen(text), &font, PointF(20, 100), &sbrush);
	graphics.DrawString(author, wcslen(author), &font, PointF(20, 320), &sbrush);
}

void COM_INIT(){
	CoInitialize(0);
}

void COM_UNIN(){
	CoUninitialize();
}

int ConnectEvent(IUnknown* Container, REFIID riid, IUnknown* Advisor, IConnectionPointContainer** picpc, IConnectionPoint** picp)
{
	HRESULT hr = 0;
	unsigned long tid = 0;
	IConnectionPointContainer* icpc = 0;
	IConnectionPoint* icp = 0;
	*picpc = 0;
	*picp = 0;

	Container->QueryInterface(IID_IConnectionPointContainer, (void **)&icpc);
	if (icpc)
	{
		*picpc = icpc;
		icpc->FindConnectionPoint(riid, &icp);
		if (icp)
		{
			*picp = icp;
			hr = icp->Advise(Advisor, &tid);
		}
	}
	return tid;
}

void disconnect(){
	AddText(infoLog, "\r\nSTOPPING...");
	if (session){
		session->Close();
		session->Release();
		session = NULL;
		AddText(infoLog, "\r\nSession stopped!");
	}
	else
		AddText(infoLog, "\r\nError stopping: No active session!");
}

void share_and_invite(){
	if (session == NULL){

		if (CoCreateInstance(__uuidof(RDPSession),
			NULL, CLSCTX_INPROC_SERVER,
			__uuidof(IRDPSRAPISharingSession),
			(void**)&session) == S_OK){
			AddText(infoLog, "\r\nInstance created!\r\n");
			ConnectEvent((IUnknown*)session, __uuidof(_IRDPSessionEvents), (IUnknown*)&ev, &picpc, &picp);

			if (session->Open() == S_OK){
				AddText(infoLog, "Session opened!\r\n");
				if (session->get_Invitations(&invitationManager) == S_OK){
					AddText(infoLog, "Get invitations ok!\r\n");

					if (invitationManager->CreateInvitation(
						L"WinPresenter",
						L"PresentationGroup",
						L"",
						MAX_ATTENDEE,
						&invitation) == S_OK){
						AddText(infoLog, "Invitation obtained!\r\n");

						saveTo:
						char *INVITATION_FILE = saveFile();
						if (INVITATION_FILE){

							FILE *invite = fopen(INVITATION_FILE, "w");
							if (invite){
								BSTR inviteString;
								if (invitation->get_ConnectionString(&inviteString) == S_OK){
									fwprintf_s(invite, L"%ws", inviteString);
									AddText(infoLog, "Invitation written to file!\r\n");
									AddText(infoLog, dupcat("Path: ", INVITATION_FILE, "\r\n", 0));
									SysFreeString(inviteString);
								}
								fclose(invite);
							}

							if (session->get_Attendees(&attendeeManager) == S_OK){
								AddText(infoLog, "Get Attendees ok!\r\n");
								AddText(infoLog, "WAITING FOR ATTENDEES!\r\n");
							}
						}
						else{
							MessageBox(hwnd, "A folder to save the invitation is required!", 0, 0);
							goto saveTo;
						}
					}
					else
						AddText(infoLog, "Error obtaining invitation!\r\n");
				}
				else
					AddText(infoLog, "Get invitations error!\r\n");
			}
			else
				AddText(infoLog, "Error opening session!\r\n");
		}
		else
			AddText(infoLog, "Error creating instance!\r\n");
	}
	else
		AddText(infoLog, "\r\nError starting: Session already exists!");
}

void OnAttendeeConnected(IDispatch *pAttendee){
	IRDPSRAPIAttendee *pRDPAtendee;
	pAttendee->QueryInterface(__uuidof(IRDPSRAPIAttendee), (void**)&pRDPAtendee);
	pRDPAtendee->put_ControlLevel(CTRL_LEVEL::CTRL_LEVEL_VIEW);
	AddText(infoLog, "An attendee connected!\r\n");
}

void OnAttendeeDisconnected(IDispatch *pAttendee){
	IRDPSRAPIAttendeeDisconnectInfo *info;
	ATTENDEE_DISCONNECT_REASON reason;
	pAttendee->QueryInterface(__uuidof(IRDPSRAPIAttendeeDisconnectInfo), (void**)&info);
	if (info->get_Reason(&reason) == S_OK){
		char *textReason = NULL;
		switch (reason){
		case ATTENDEE_DISCONNECT_REASON_APP:
			textReason = "Viewer terminated session!";
			break;
		case ATTENDEE_DISCONNECT_REASON_ERR:
			textReason = "Internal Error!";
			break;
		case ATTENDEE_DISCONNECT_REASON_CLI:
			textReason = "Attendee requested termination!";
			break;
		default:
			textReason = "Unknown reason!";
		}
		AddText(infoLog, dupcat("Attendee disconnected\r\n   Reason: ", textReason, "\r\n", 0));
	}
	pAttendee->Release();
	picp = 0;
	picpc = 0;
	//disconnect();
}

void OnControlLevelChangeRequest(IDispatch  *pAttendee, CTRL_LEVEL RequestedLevel){
	IRDPSRAPIAttendee *pRDPAtendee;
	pAttendee->QueryInterface(__uuidof(IRDPSRAPIAttendee), (void**)&pRDPAtendee);
	if (pRDPAtendee->put_ControlLevel(RequestedLevel) == S_OK){
		switch (RequestedLevel){
		case CTRL_LEVEL_NONE:
			AddText(infoLog, "Level changed to CTRL_LEVEL_NONE!\r\n");
			break;
		case CTRL_LEVEL_VIEW:
			AddText(infoLog, "Level changed to CTRL_LEVEL_VIEW!\r\n");
			break;
		case CTRL_LEVEL_INTERACTIVE:
			AddText(infoLog, "Level changed to CTRL_LEVEL_INTERACTIVE!\r\n");
			break;
		}
	}
}

HWND CreateButton(LPCSTR lpButtonName, HWND hWnd, int x, int y){
	return CreateWindow("button", lpButtonName, WS_EX_TRANSPARENT | BS_OWNERDRAW | WS_CHILD | WS_VISIBLE, x, y, 100, 30, hWnd, 0, (HINSTANCE)hWnd, 0);
}

int WINAPI WinMain(HINSTANCE hInstance,
	HINSTANCE hPrevInstance,
	LPSTR lpCmdLine,
	int nCmdShow)
{
	WNDCLASSEX WndClass;
	MSG Msg;
	instance = hInstance;

	WndClass.cbSize = sizeof(WNDCLASSEX);
	WndClass.style = NULL;
	WndClass.lpfnWndProc = WndProc;
	WndClass.cbClsExtra = 0;
	WndClass.cbWndExtra = 0;
	WndClass.hInstance = instance;
	WndClass.hIcon = LoadIcon(hInstance, APP);
	WndClass.hCursor = LoadCursor(NULL, IDC_ARROW);
	WndClass.hbrBackground = CreateSolidBrush(RGB(245, 247, 248));
	WndClass.lpszMenuName = 0;
	WndClass.lpszClassName = className;
	WndClass.hIconSm = LoadIcon(hInstance, APPSMALL);

	RegisterClassEx(&WndClass);
	hwnd = CreateWindowEx(
		0,
		className,
		"REMOTE DESKTOP SHARING - INVITER",
		WS_OVERLAPPEDWINDOW,
		CW_USEDEFAULT, CW_USEDEFAULT,
		400, 400,
		NULL, NULL,
		instance,
		NULL);

	startSharing = CreateButton("Start Sharing", hwnd, 20, 20);
	stopSharing = CreateButton("Stop Sharing", hwnd, 230, 20);
	infoLog = CreateWindow("edit", 0, WS_CHILD | WS_VISIBLE | ES_MULTILINE | WS_VSCROLL | ES_AUTOVSCROLL, 20, 150, 350, 150, hwnd, 0, instance, 0);

	ShowWindow(hwnd, 1);
	UpdateWindow(hwnd);

	SendMessage(infoLog, EM_SETREADONLY, 1, 0);
	SetWindowLong(hwnd, GWL_STYLE, GetWindowLong(hwnd, GWL_STYLE) &~WS_MAXIMIZEBOX);

	while (GetMessage(&Msg, NULL, 0, 0)) {
		TranslateMessage(&Msg);
		DispatchMessage(&Msg);
	}
	return Msg.wParam;
}

LRESULT CALLBACK WndProc(HWND hwnd, UINT Message, WPARAM wParam, LPARAM lParam) {
	HDC hdc;
	PAINTSTRUCT ps;

	GdiplusStartupInput gdiplusStartupInput;
	ULONG_PTR           gdiplusToken;
	GdiplusStartup(&gdiplusToken, &gdiplusStartupInput, NULL);

	switch (Message) {
	case WM_PAINT:
		hdc = BeginPaint(hwnd, &ps);
		COM_INIT();
		GDIPLUS(hdc);
		EndPaint(hwnd, &ps);
		break;
	case WM_DRAWITEM:
	{
		LPDRAWITEMSTRUCT pDIS;
		pDIS = (LPDRAWITEMSTRUCT)lParam;
		
		CHAR staticText[99];
		int len = SendMessage(pDIS->hwndItem, WM_GETTEXT, ARRAYSIZE(staticText), (LPARAM)staticText);
		
		if (pDIS->hwndItem == infoLog){
			DrawTextA(pDIS->hDC, staticText, strlen(staticText), &pDIS->rcItem, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
		}
		if (pDIS->hwndItem == startSharing || pDIS->hwndItem == stopSharing){
			SetBkMode(pDIS->hDC, TRANSPARENT);
			FillRect(pDIS->hDC, &pDIS->rcItem, CreateSolidBrush(RGB(0, 100, 200)));
			SetTextColor(pDIS->hDC, RGB(255, 255, 255));
			DrawTextA(pDIS->hDC, staticText, strlen(staticText), &pDIS->rcItem, DT_CENTER | DT_SINGLELINE | DT_VCENTER);
			SetTextColor(pDIS->hDC, RGB(0, 0, 0));
			SelectObject(pDIS->hDC, GetStockObject(NULL_BRUSH));
			SelectObject(pDIS->hDC, CreatePen(PS_DOT, 1, RGB(255, 255, 255)));

			if (pDIS->itemAction & ODA_SELECT)
				SelectObject(pDIS->hDC, CreatePen(PS_DOT, 1, RGB(255, 255, 255)));
			else
				SelectObject(pDIS->hDC, CreatePen(PS_SOLID, 1, RGB(255, 255, 255)));
			Rectangle(
				pDIS->hDC,
				pDIS->rcItem.left,
				pDIS->rcItem.top,
				pDIS->rcItem.right,
				pDIS->rcItem.bottom
				);
		}
	}
		break;
	case WM_COMMAND:
		if ((HWND)lParam == startSharing){
			share_and_invite();
		}
		if ((HWND)lParam == stopSharing){
			disconnect();
		}
		break;

	case WM_CLOSE:
		COM_UNIN();
		DestroyWindow(hwnd);
		break;
	case WM_DESTROY:
		PostQuitMessage(0);
		break;
	default:
		return DefWindowProc(hwnd, Message, wParam, lParam);
	}
	return 0;
}