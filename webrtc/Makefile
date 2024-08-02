SFU_SRC=sfu/main.go
PEER_SRC=peer/main.go peer/packet.go peer/proxy_connection.go peer/signaling.go peer/transcoder.go

UNAME=$(shell uname)
ARCH=$(shell arch)
SFU_EXE=sfu-$(UNAME)-$(ARCH).exe
PEER_EXE=peer-$(UNAME)-$(ARCH).exe
ALL=$(SFU_EXE) $(PEER_EXE)

# Need something for Windows, if we have Make there.

all: $(ALL)
	echo Built all for $(UNAME)

$(SFU_EXE): $(SFU_SRC)
	go build -o $(SFU_EXE) ./sfu
	
$(PEER_EXE): $(PEER_SRC)
	go build -o $(PEER_EXE) ./peer