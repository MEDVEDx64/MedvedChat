#!/usr/bin/env python

import curses
import tpmp
import cmd
import sys

from datetime import datetime

class KlausClient(tpmp.TPClient):
    def print_text(self, text):
        print(datetime.now().strftime('%H:%M:%S ') + text)

    def on_log(self, text):
        self.print_text('--- ' + text)

    def on_notification(self, text):
        self.print_text(text)

    def on_message(self, text, sender):
        s = sender
        if s == None:
            s = '<Anonymous>'
        self.print_text('[' + s + ']: ' + text)

    def on_userlist(self, users):
        self.users = users

    def _client_init(self):
        # Parsing address
        addr = sys.argv[1]
        port = tpmp.PORT
        if(addr.find(':') >= 0):
            addr = addr.split(':')
            port = int(addr[1])
            addr = addr[0]

        # Connect and login with nickname
        self.connect(addr, port)
        self.send_text(sys.argv[2], tpmp.MSG_PAYLOAD_CLI_LOGIN)

    def _client_close(self):
        self.socket.close()

    def __init__(self):
        self.users = []
        self._client_init()

    def shutdown(self):
        self._client_close()

    def on_disconnected(self):
        sys.exit(0)

def klaus_main():
    if(len(sys.argv) < 3):
        print('usage: ' + sys.argv[0] + ' server[:port] nickname')
        return

    kc = KlausClient()

    # Main loop
    try:
        while True:
            i = ''
            try:
                i = raw_input()
                # Erasing user input that will be replaced
                # by server response
                sys.stdout.write('\033[1A\033[2K')
                sys.stdout.flush()
            except:
                break
            if len(i) == 0: continue
            if i[0] == '/':
                i = i[1:]
                i = i.split(' ')

                if i[0] == 'exit' or i[0] == 'quit' or i[0] == 'q':
                    break
                elif i[0] == 'update' or i[0] == 'u':
                    kc.send_text(None, tpmp.MSG_PAYLOAD_CLI_USERLIST)
                elif i[0] == 'list' or i[0] == 'users' or i[0] == 'ls':
                    kc.on_log('Userlist')
                    for u in kc.users:
                        kc.on_log('~ ' + u)
                    kc.on_log('Userlist (end)')
                else:
                    kc.on_log('Unknown command')

            else:
                kc.send_text(i)

    finally:
        kc.shutdown()

if __name__ == '__main__':
    klaus_main()
