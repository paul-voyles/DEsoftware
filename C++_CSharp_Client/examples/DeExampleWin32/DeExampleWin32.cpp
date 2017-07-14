#include <iostream>
#include <time.h>
//#include <DeInterface.Win32.h>
#include "C:\Users\Chenyu\Documents\GitHub\DEsoftware\C++_CSharp_Client\include\DeInterface.Win32.h"

bool chooseCamera(DeInterfaceWin32 *server)
{
	// query the list of available cameras
	std::vector<std::string> cameras;
	if (!server->getCameraNames(cameras)) {
		std::cerr << "Unable to get list of available cameras" << std::endl;
		return false;
	}

	// show the available cameras and allow user to select model
	std::cout << std::endl << "--Camera Select--" << std::endl;
	unsigned int selectedCamera = (unsigned int)cameras.size();
	for (unsigned int i = 0; i < cameras.size(); i++) {
		std::cout << i + 1 << ": " << cameras.at(i) << std::endl;
	}
	std::cout << "0: Quit" << std::endl;
	std::cout << std::endl << "Please choose a model: ";
	std::cin >> selectedCamera;
	if (selectedCamera < 1 || selectedCamera > cameras.size()) 
		return false;
	server->setCameraName(cameras.at(selectedCamera - 1).c_str());

	return true;
}

void showProperties(DeInterfaceWin32 *server)
{
	std::vector<std::string> properties;
	if (!server->getProperties(properties)) {
	//	std::cerr << "Error getting properties" << std::endl;
		return;
	}

	std::string value;
	for (unsigned int i = 0; i < properties.size(); i++) {
		value.clear();
		if (!server->getProperty(properties.at(i), value)) {
			std::cerr << "Error retrieving property value " << i << std::endl;
			return;
		}
		std::cout << properties.at(i) << ": " << value << std::endl << std::flush;
	}
}

void getImage(DeInterfaceWin32 *server)
{
	int xsize, ysize;
	if (!server->getTypedProperty("Image Size X", xsize)) {
		std::cerr << "Unable to get image x dimensions" << std::endl;
		return;
	}
	if (!server->getTypedProperty("Image Size Y", ysize)) {
		std::cerr << "Unable to get image y dimensions" << std::endl;
		return;
	}

	unsigned short *imgBuffer = new unsigned short [xsize * ysize];
	if (imgBuffer == NULL) {
		std::cerr << "Unable to allocate memory for image buffer" << std::endl;
		return;
	}

	clock_t startTime = clock(); //Start timer
	double secondsPassed;

	if (!server->getImage(imgBuffer, xsize * ysize * sizeof(unsigned short))) {
		std::cerr << "Unable to get image\n";
	} else {
		secondsPassed = (clock() - startTime) / (double)CLOCKS_PER_SEC;
		std::cout << "Image grabbed successfully in " << secondsPassed << " seconds!" << std::endl;
	}

	delete []imgBuffer;
}

bool chooseOption(DeInterfaceWin32 *server)
{
	std::string cameraName;
	if (!server->getSelectedCamera(cameraName)) {
		std::cerr << "Error, no camera selected" << std::endl;
		return false;
	}

	int action;
	std::cout << std::endl << "--" << cameraName << "--" << std::endl;
	std::cout << "1: Show Properties" << std::endl;
	std::cout << "2: Grab Image" << std::endl;
	std::cout << "0: Return" << std::endl;
	std::cout << std::endl << "Choose an action: ";
	std::cin >> action;

	switch (action) {
		case 1: 
			showProperties(server);
			break;

		case 2:
			getImage(server);
			break;

		default:
			return false;
	}

	return true;
}

int main(int argc, char* argv[])
{
	DeInterfaceWin32 cameraServer;

	// connect to the server
	if (!cameraServer.connect("127.0.0.1", 48880, 48879)) {
		std::cerr << "Unable to connect to camera server" << std::endl;
		std::cout << "Unable to connect to camera server" << std::endl;
		return -1;
	}

	// pick an action
	while (chooseCamera(&cameraServer)) {
		while (chooseOption(&cameraServer)) {}
	}

	return 0;
}

