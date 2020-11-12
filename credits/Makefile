.PHONY: all

all: Credits.html

%.html: %.md
	pandoc -s $< -o $@

Credits.html: Credits.md