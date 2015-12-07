import socket

from threading import Thread

PORT = 11070

MSG_TYPE_TEXT = 1
MSG_TYPE_RAW = 0

MSG_PAYLOAD_CLI_TEXT = 0
MSG_PAYLOAD_CLI_LOGIN = 1
MSG_PAYLOAD_CLI_PRIVATE = 2
MSG_PAYLOAD_CLI_USERLIST = 3

MSG_PAYLOAD_SRV_NOTIFY = 32
MSG_PAYLOAD_SRV_TEXT = 33
MSG_PAYLOAD_SRV_USERLIST = 34

# Base class for client application
class TPClient(object):
    def __init__(self):
        self.socket = None

    def on_log(self, text): pass
    def on_notification(self, text): pass
    def on_message(self, text, sender): pass
    def on_userlist(self, users): pass

    # Invoked when connection closed
    def on_disconnected(self): pass

    # Connect to server and create message listening thread
    def connect(self, address, port = PORT):
        self.socket = socket.socket()
        self.on_log("Connecting to " + address)
        self.socket.connect((address, port))
        self.on_log("Connected")

        def _connect(address, port):
            try:
                while True:
                    # Message receiving and parsing goes here
                    data = self.socket.recv(0x4003)
                    if len(data) == 0: break

                    header = bytearray(data[:3])
                    m_payload = int(header[0]) >> 1
                    m_type = int(header[0]) & 1
                    m_length = (header[1] << 8) | header[2]
                    if len(data) < m_length:
                        self.on_log("Malformed message")
                        continue
                    text = data[3:m_length+3]

                    if m_type == MSG_TYPE_RAW:
                        self.on_log("Incoming raw/shit message")
                        continue

                    if m_payload == MSG_PAYLOAD_SRV_TEXT:
                        sender = None
                        try:
                            nl = text.index('\n')
                            sender = text[:nl]
                            text = text[nl+1:]
                        except ValueError:
                            pass
                        self.on_message(text, sender)

                    elif m_payload == MSG_PAYLOAD_SRV_NOTIFY:
                        self.on_notification(text)

                    elif m_payload == MSG_PAYLOAD_SRV_USERLIST:
                        users = []
                        for u in text.split('\n'):
                            if len(u) == 0: continue
                            users.append(u)
                        self.on_userlist(users)

                    else:
                        self.on_log("Unknown text message received")
            except:
                self.on_log("Connection terminated.")
                self.on_disconnected()

        Thread(target = _connect, args = (address, port)).start()

    # Message sending
    def send_raw(self, m_type, m_payload, text):
        tx = text
        if tx == None:
            tx = ''

        header = bytearray(3)
        ln = len(tx)
        if ln >= 0x4000:
            self.on_log("Message is too large")
            return

        header[0] = m_type | (m_payload << 1)
        header[1] = (ln >> 8) & 0xff
        header[2] = ln & 0xff

        self.socket.send(str(header) + tx)

    def send_text(self, text, payload = MSG_PAYLOAD_CLI_TEXT):
        self.send_raw(MSG_TYPE_TEXT, payload, text)
