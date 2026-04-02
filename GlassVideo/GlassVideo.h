#pragma once
#include "resource.h"
#include <queue>
#include <mutex>
#include <string>
#include "SlotManager.h"
#include "PipeManager.h"
#include "Consumers\ConsumerManager.h"

extern std::queue<std::string>  g_commandQueue;
extern std::mutex               g_commandMutex;
extern SlotManager              g_slotManager;
extern PipeManager				g_pipeManager;
extern ConsumerManager          g_consumerManager;