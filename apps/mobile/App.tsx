import React from 'react';
import { ActivityIndicator, View } from 'react-native';
import { NavigationContainer, DefaultTheme } from '@react-navigation/native';
import { createNativeStackNavigator } from '@react-navigation/native-stack';
import { createBottomTabNavigator } from '@react-navigation/bottom-tabs';
import { AuthProvider, useAuth } from './src/AuthContext';
import { LoginScreen } from './src/screens/LoginScreen';
import { RegisterScreen } from './src/screens/RegisterScreen';
import { HomeScreen } from './src/screens/HomeScreen';
import { SubcontractorListScreen } from './src/screens/SubcontractorListScreen';
import { SubcontractorDetailScreen } from './src/screens/SubcontractorDetailScreen';
import { AddSubcontractorScreen } from './src/screens/AddSubcontractorScreen';
import { AddDocumentScreen } from './src/screens/AddDocumentScreen';
import { ChaseQueueScreen } from './src/screens/ChaseQueueScreen';
import { SettingsScreen } from './src/screens/SettingsScreen';
import { colours } from './src/theme';
import type {
  AuthStackParamList,
  ChaseStackParamList,
  HomeStackParamList,
  SubsStackParamList
} from './src/navigationTypes';

const AuthStack = createNativeStackNavigator<AuthStackParamList>();
const HomeStack = createNativeStackNavigator<HomeStackParamList>();
const SubsStack = createNativeStackNavigator<SubsStackParamList>();
const ChaseStack = createNativeStackNavigator<ChaseStackParamList>();
const Tabs = createBottomTabNavigator();

const navTheme = {
  ...DefaultTheme,
  colors: {
    ...DefaultTheme.colors,
    background: colours.paper,
    primary: colours.navy,
    card: colours.white,
    text: colours.navy,
    border: colours.line
  }
};

function HomeNavigator() {
  return (
    <HomeStack.Navigator screenOptions={stackOptions}>
      <HomeStack.Screen name="Dashboard" component={HomeScreen} options={{ title: 'Home' }} />
      <HomeStack.Screen
        name="SubcontractorDetail"
        component={SubcontractorDetailScreen}
        options={({ route }) => ({ title: route.params.name })}
      />
      <HomeStack.Screen
        name="AddDocument"
        component={AddDocumentScreen}
        options={({ route }) => ({ title: `Document · ${route.params.name}` })}
      />
    </HomeStack.Navigator>
  );
}

function SubsNavigator() {
  return (
    <SubsStack.Navigator screenOptions={stackOptions}>
      <SubsStack.Screen name="SubcontractorList" component={SubcontractorListScreen} options={{ title: 'Subcontractors' }} />
      <SubsStack.Screen
        name="SubcontractorDetail"
        component={SubcontractorDetailScreen}
        options={({ route }) => ({ title: route.params.name })}
      />
      <SubsStack.Screen name="AddSubcontractor" component={AddSubcontractorScreen} options={{ title: 'Add subcontractor' }} />
      <SubsStack.Screen
        name="AddDocument"
        component={AddDocumentScreen}
        options={({ route }) => ({ title: `Document · ${route.params.name}` })}
      />
    </SubsStack.Navigator>
  );
}

function ChaseNavigator() {
  return (
    <ChaseStack.Navigator screenOptions={stackOptions}>
      <ChaseStack.Screen name="ChaseQueue" component={ChaseQueueScreen} options={{ title: 'Chase queue' }} />
      <ChaseStack.Screen
        name="SubcontractorDetail"
        component={SubcontractorDetailScreen}
        options={({ route }) => ({ title: route.params.name })}
      />
      <ChaseStack.Screen
        name="AddDocument"
        component={AddDocumentScreen}
        options={({ route }) => ({ title: `Document · ${route.params.name}` })}
      />
    </ChaseStack.Navigator>
  );
}

const stackOptions = {
  headerStyle: { backgroundColor: colours.navy },
  headerTintColor: colours.white,
  headerTitleStyle: { fontWeight: '700' as const }
};

function SignedInTabs() {
  return (
    <Tabs.Navigator
      screenOptions={{
        headerShown: false,
        tabBarActiveTintColor: colours.navy,
        tabBarInactiveTintColor: colours.muted,
        tabBarStyle: { backgroundColor: colours.white, borderTopColor: colours.line }
      }}
    >
      <Tabs.Screen name="HomeTab" component={HomeNavigator} options={{ title: 'Home' }} />
      <Tabs.Screen name="SubsTab" component={SubsNavigator} options={{ title: 'Subs' }} />
      <Tabs.Screen name="ChaseTab" component={ChaseNavigator} options={{ title: 'Chase' }} />
      <Tabs.Screen name="SettingsTab" component={SettingsScreen} options={{ title: 'Settings', headerShown: true, ...stackOptions }} />
    </Tabs.Navigator>
  );
}

function RootNavigation() {
  const { ready, user } = useAuth();

  if (!ready) {
    return (
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', backgroundColor: colours.paper }}>
        <ActivityIndicator color={colours.navy} />
      </View>
    );
  }

  return (
    <NavigationContainer theme={navTheme}>
      {user ? (
        <SignedInTabs />
      ) : (
        <AuthStack.Navigator screenOptions={stackOptions}>
          <AuthStack.Screen name="Login" component={LoginScreen} options={{ headerShown: false }} />
          <AuthStack.Screen name="Register" component={RegisterScreen} options={{ title: 'Register' }} />
        </AuthStack.Navigator>
      )}
    </NavigationContainer>
  );
}

export default function App() {
  return (
    <AuthProvider>
      <RootNavigation />
    </AuthProvider>
  );
}
