// Usage: injector.exe <path to ACMirage.exe>
// Launches the game suspended, injects mirage_vr.dll (next to this exe) via LoadLibrary, resumes.
#include <windows.h>
#include <cstdio>
#include <string>

int main(int argc, char** argv) {
    if (argc < 2) { puts("usage: injector.exe <game.exe>"); return 1; }
    char self[MAX_PATH]; GetModuleFileNameA(nullptr, self, MAX_PATH);
    std::string dll = std::string(self);
    dll = dll.substr(0, dll.find_last_of("\\/") + 1) + "mirage_vr.dll";

    std::string game = argv[1];
    std::string dir = game.substr(0, game.find_last_of("\\/"));
    STARTUPINFOA si{sizeof si}; PROCESS_INFORMATION pi{};
    if (!CreateProcessA(game.c_str(), nullptr, nullptr, nullptr, FALSE, CREATE_SUSPENDED,
                        nullptr, dir.c_str(), &si, &pi)) { puts("CreateProcess failed"); return 1; }

    void* mem = VirtualAllocEx(pi.hProcess, nullptr, dll.size() + 1, MEM_COMMIT, PAGE_READWRITE);
    WriteProcessMemory(pi.hProcess, mem, dll.c_str(), dll.size() + 1, nullptr);
    HANDLE t = CreateRemoteThread(pi.hProcess, nullptr, 0,
        (LPTHREAD_START_ROUTINE)GetProcAddress(GetModuleHandleA("kernel32"), "LoadLibraryA"), mem, 0, nullptr);
    WaitForSingleObject(t, 5000);
    ResumeThread(pi.hThread);
    return 0;
}
